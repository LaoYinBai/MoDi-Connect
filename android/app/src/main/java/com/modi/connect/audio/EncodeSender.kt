/*
 * MoDi Connect - Cross-device interconnection protocol
 * Copyright (C) 2026 Silvite
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */
package com.modi.connect.audio

import com.modi.protocol.PacketHeader

import com.modi.connect.core.adapters.UdpTransport
import com.modi.protocol.TransportType
import com.modi.connect.core.factory.PlatformFactory
import com.modi.connect.core.infrastructure.Log
import com.modi.protocol.ITransport
import com.modi.protocol.Packet
import com.modi.protocol.LinkType
import com.modi.protocol.PacketType
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.CoroutineDispatcher
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.launch
import kotlinx.coroutines.NonCancellable
import kotlinx.coroutines.withContext

internal class TransportConnector(
    private val ioDispatcher: CoroutineDispatcher = Dispatchers.IO,
) {
    suspend fun connect(transport: ITransport): Result<Unit> {
        return try {
            withContext(ioDispatcher) { transport.connect() }
            Result.success(Unit)
        } catch (cancelled: CancellationException) {
            withContext(NonCancellable + ioDispatcher) { transport.disconnect() }
            throw cancelled
        } catch (exception: Exception) {
            withContext(NonCancellable + ioDispatcher) { transport.disconnect() }
            Result.failure(exception)
        }
    }

    suspend fun disconnect(transport: ITransport) =
        withContext(ioDispatcher) { transport.disconnect() }
}

/**
 * EncodeSender — 编码 + 发送模块
 *
 * 职责：接收 PCM 帧 → 拼帧 → Opus 编码 → 协议封装 → 异步入队发送。
 * 采集线程只做编码+入队（非阻塞），发送协程独立消费队列。
 * 网络阻塞不影响采集线程。
 */
class EncodeSender(
    private val config: AudioConfig,
    ioDispatcher: CoroutineDispatcher = Dispatchers.IO,
) {

    companion object {
        private const val TAG = "EncodeSender"
        private const val SEND_QUEUE_CAPACITY = 64  // 缓冲 64 包 ≈ 1.28s
    }

    private val enc = AudioEncoder(config)
    private val protocol = PlatformFactory.createProtocol()
    private val assembler = PcmFrameAssembler(AudioPipeline.FRAME_BYTES)
    private var transport: ITransport? = null
    private var seq = 0
    private var firstFrameNotified = false
    private val connector = TransportConnector(ioDispatcher)

    // ── 发送队列（采集线程写，发送协程读） ──
    private val sendQueue = Channel<ByteArray>(SEND_QUEUE_CAPACITY)
    private val scope = CoroutineScope(Dispatchers.IO + SupervisorJob())
    private var senderJob: Job? = null

    /** 当前链路类型（由 AudioPipeline 设置） */
    @Volatile var linkType: Byte = LinkType.WIFI_LAN

    /** 首帧回调（仅触发一次） */
    var onFirstFrame: (() -> Unit)? = null

    /** 原始 Opus 回调（调试用） */
    var onOpusData: ((ByteArray, Int) -> Unit)? = null

    /** 准备编码器 + 创建 Transport + 启动发送协程 */
    suspend fun prepare(host: String?, port: Int, localBindAddress: String? = null): Boolean {
        if (!enc.prepare()) return false
        if (host != null) {
            try {
                val t = PlatformFactory.createTransport(
                    type = TransportType.Udp,
                    host = host,
                    port = port,
                    localBindAddress = localBindAddress
                ) as UdpTransport
                connector.connect(t).getOrThrow()
                transport = t
            } catch (e: Exception) {
                Log.e(TAG, "Transport connect failed: ${e.message}")
                enc.release()
                return false
            }
        }
        startSender()
        return true
    }

    /**
     * 使用外部 Transport 准备（蓝牙/USB 链路用）。
     * 不创建 UdpTransport，直接使用传入的已连接 ITransport。
     */
    fun prepareWithTransport(externalTransport: ITransport): Boolean {
        if (!enc.prepare()) return false
        transport = externalTransport
        startSender()
        return true
    }

    /** 启动发送协程：从队列读取编码包并发送到 Transport */
    private fun startSender() {
        senderJob = scope.launch {
            for (data in sendQueue) {
                try {
                    transport?.send(data)
                } catch (e: Exception) {
                    Log.e(TAG, "Send error: ${e.message}")
                }
            }
        }
    }

    /** 喂入一帧 PCM（可能不是 1920 字节，内部拼帧）。采集线程调用，非阻塞。 */
    fun feed(pcm: ByteArray) {
        assembler.push(pcm) { frame ->
            val opus = enc.encodeFrame(frame)
            if (opus != null) {
                val packet = Packet(PacketType.AUDIO, linkType, seq.toUInt(), opus)
                val encoded = protocol.encode(packet)

                // 非阻塞入队：队列满时丢弃最旧包
                if (!sendQueue.trySend(encoded).isSuccess) {
                    sendQueue.tryReceive()  // 丢弃最旧
                    sendQueue.trySend(encoded)
                }

                if (!firstFrameNotified) {
                    firstFrameNotified = true
                    onFirstFrame?.invoke()
                }
                onOpusData?.invoke(opus, seq)
                seq++
            }
        }
    }

    /** 重置拼帧器 + 序号 + 清空发送队列（新会话/切模式时） */
    fun reset() {
        assembler.reset()
        seq = 0
        firstFrameNotified = false
        // 清空队列中残留的旧包，避免切模式后发送过期数据
        while (sendQueue.tryReceive().isSuccess) { }
    }

    /** 释放编码器 + 停止发送协程 + Transport */
    suspend fun release() {
        senderJob?.cancel()
        senderJob = null
        transport?.let { t ->
            // 蓝牙/USB Transport 由链路管理生命周期，此处不主动断开
            if (t is UdpTransport) connector.disconnect(t)
        }
        transport = null
        enc.release()
    }
}
