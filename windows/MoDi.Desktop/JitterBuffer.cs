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
using System;
using System.Collections.Generic;

using MoDi.Protocol;

namespace MoDi.Desktop;

/// <summary>
/// JitterBuffer — 音频帧抖动缓冲
///
/// ## 职责
/// 消除 UDP 网络抖动导致的音频卡顿：
/// 1. 按序列号排序缓存到达的 PCM 帧
/// 2. 预缓冲（PreFill）：启动时攒满 N 帧再开始输出，消除冷启动卡顿
/// 3. 匀速输出：由外部 20ms 定时器驱动 Pull，保证播放节奏稳定
/// 4. 溢出保护：缓冲满时丢弃最老帧
///
/// ## 线程安全
/// Push 由网络回调线程调用，Pull 由定时器线程调用，内部加锁。
/// </summary>
public sealed class JitterBuffer
{
    private readonly int _capacity;       // 最大缓冲帧数
    private readonly int _preFillCount;   // 预缓冲帧数（攒满后才开始输出）
    private readonly object _lock = new();

    // 按 seq 排序的帧队列
    private readonly SortedDictionary<uint, float[]> _buffer = new();
    private uint _nextPullSeq;          // 下一个期望输出的 seq
    private bool _hasNextSeq;
    private bool _prefilled;              // 是否已完成预缓冲

    // 诊断计数
    private int _pushOkCount;
    private int _pushLateCount;
    private int _pushDupCount;
    private int _pullGapCount;
    private int _consecutiveUnderrun;   // 连续 underrun 计数（容忍短暂帧到达延迟）
    private const int UnderrunSkipThreshold = 5;  // 正常模式：连续 5 次 underrun（100ms）后跳过
    private const int ColdStartUnderrunThreshold = 25; // 冷启动模式：连续 25 次（500ms）后跳过
    private int _pullsSincePrefill;     // prefill 后已拉取次数（用于冷启动检测）
    private const int ColdStartPulls = 100; // 前 100 次拉取（≈2s）视为冷启动阶段

    /// <summary>缓冲下溢次数（Pull 时无帧可取）</summary>
    public int UnderrunCount { get; private set; }

    /// <summary>溢出丢帧次数（缓冲满时丢弃）</summary>
    public int OverflowCount { get; private set; }

    /// <summary>当前缓冲帧数</summary>
    public int Count { get { lock (_lock) return _buffer.Count; } }

    /// <summary>是否已完成预缓冲（开始正常输出）</summary>
    public bool IsPrefilled => _prefilled;

    /// <param name="capacity">最大缓冲帧数（默认 5，即 100ms）</param>
    /// <param name="preFillCount">预缓冲帧数（默认 3，即 60ms）</param>
    public JitterBuffer(int capacity = 5, int preFillCount = 3)
    {
        _capacity = capacity;
        _preFillCount = preFillCount;
    }

    /// <summary>
    /// 推入一帧解码后的 PCM（由网络回调线程调用）
    /// </summary>
    /// <param name="seq">帧序列号</param>
    /// <param name="pcm">解码后的 float32 PCM 数据</param>
    public void Push(uint seq, float[] pcm)
    {
        lock (_lock)
        {
            // 检测新会话：seq 大幅回退（>100 帧 = 2s）表示 Android 重新推流
            if (_hasNextSeq && SequenceHelper.Before(seq, _nextPullSeq))
            {
                uint backward = SequenceHelper.Backward(seq, _nextPullSeq);
                if (backward > 100)
                {
                    // 新会话：重置缓冲，接受新帧
                    _buffer.Clear();
                    _prefilled = false;
                    _hasNextSeq = false;
                }
                else
                {
                    _pushLateCount++;
                    if (_pushLateCount <= 5 || _pushLateCount % 200 == 0)
                        MoDi.Core.Infrastructure.Log.W("JitterBuffer",
                            $"Push DISCARD late: seq={seq}, nextPull={_nextPullSeq}, backward={backward}, count={_pushLateCount}");
                    return; // 真正的迟到包，丢弃
                }
            }

            // 丢弃重复帧
            if (_buffer.ContainsKey(seq))
            {
                _pushDupCount++;
                if (_pushDupCount <= 3)
                    MoDi.Core.Infrastructure.Log.W("JitterBuffer", $"Push DISCARD dup: seq={seq}");
                return;
            }

            // 溢出保护：缓冲满时丢弃最老帧
            if (_buffer.Count >= _capacity)
            {
                var oldest = _buffer.Keys.GetEnumerator();
                if (oldest.MoveNext())
                {
                    _buffer.Remove(oldest.Current);
                    OverflowCount++;
                }
            }

            _buffer[seq] = pcm;
            _pushOkCount++;
            if (_pushOkCount <= 3 || _pushOkCount % 500 == 0)
                MoDi.Core.Infrastructure.Log.I("JitterBuffer",
                    $"Push ok: seq={seq}, bufCount={_buffer.Count}, prefilled={_prefilled}, nextPull={_nextPullSeq}");

            // 检查预缓冲是否完成
            if (!_prefilled && _buffer.Count >= _preFillCount)
            {
                _prefilled = true;
                // 设定起始输出 seq 为缓冲中最小的 seq
                var first = _buffer.Keys.GetEnumerator();
                if (first.MoveNext())
                {
                    _nextPullSeq = first.Current;
                    _hasNextSeq = true;
                }
            }
        }
    }

    /// <summary>
    /// 取出一帧 PCM（由 20ms 定时器线程调用）
    /// 返回 null 表示缓冲空（underrun），播放端应输出静音
    /// </summary>
    public float[]? Pull()
    {
        lock (_lock)
        {
            if (!_prefilled) return null;

            if (_buffer.TryGetValue(_nextPullSeq, out var pcm))
            {
                _buffer.Remove(_nextPullSeq);
                _nextPullSeq++;
                _consecutiveUnderrun = 0;  // 成功拉取，重置计数
                _pullsSincePrefill++;
                return pcm;
            }

            // 期望的 seq 不在缓冲中（丢包或乱序）
            // 如果缓冲中有更后面的帧，跳过空洞
            if (_buffer.Count > 0)
            {
                var first = _buffer.Keys.GetEnumerator();
                first.MoveNext();
                uint oldest = first.Current;

                // 如果空洞超过 capacity/2，说明 seq 跳变（可能重连），重置
                uint gap = SequenceHelper.Distance(_nextPullSeq, oldest);
                _pullGapCount++;
                if (_pullGapCount <= 5 || _pullGapCount % 200 == 0)
                    MoDi.Core.Infrastructure.Log.W("JitterBuffer",
                        $"Pull GAP: want={_nextPullSeq}, oldest={oldest}, gap={gap}, bufCount={_buffer.Count}, count={_pullGapCount}");
                if (gap > _capacity)
                {
                    _nextPullSeq = oldest;
                    if (_buffer.TryGetValue(_nextPullSeq, out var p))
                    {
                        _buffer.Remove(_nextPullSeq);
                        _nextPullSeq++;
                        return p;
                    }
                }
            }

            // Underrun：无帧可取
            UnderrunCount++;
            _consecutiveUnderrun++;
            // 冷启动阶段用更高阈值（HAL 帧到达间隔可达 100-200ms），
            // 避免 _nextPullSeq 飞速前进导致后续帧被当作“迟到包”丢弃。
            int threshold = _pullsSincePrefill < ColdStartPulls
                ? ColdStartUnderrunThreshold
                : UnderrunSkipThreshold;
            if (_consecutiveUnderrun >= threshold)
            {
                _nextPullSeq++;
                _consecutiveUnderrun = 0;
            }
            return null;
        }
    }

    /// <summary>重置缓冲（停止/重连时调用）</summary>
    public void Reset()
    {
        lock (_lock)
        {
            _buffer.Clear();
            _prefilled = false;
            _hasNextSeq = false;
            _nextPullSeq = 0;
            _consecutiveUnderrun = 0;
            _pullsSincePrefill = 0;
            UnderrunCount = 0;
            OverflowCount = 0;
        }
    }

}
