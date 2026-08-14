package com.modi.connect.ui.link

import android.annotation.SuppressLint
import androidx.camera.core.CameraSelector
import androidx.camera.core.ImageAnalysis
import androidx.camera.core.ImageProxy
import androidx.camera.core.Preview
import androidx.camera.lifecycle.ProcessCameraProvider
import androidx.camera.view.PreviewView
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import androidx.compose.ui.viewinterop.AndroidView
import androidx.core.content.ContextCompat
import androidx.lifecycle.compose.LocalLifecycleOwner
import com.google.mlkit.vision.barcode.BarcodeScanner
import com.google.mlkit.vision.barcode.BarcodeScannerOptions
import com.google.mlkit.vision.barcode.BarcodeScanning
import com.google.mlkit.vision.barcode.common.Barcode
import com.google.mlkit.vision.common.InputImage
import com.modi.connect.core.infrastructure.Log
import java.net.URLDecoder
import java.nio.charset.StandardCharsets
import java.util.concurrent.Executors

data class MoDiQrCode(
    val transport: String,
    val deviceName: String,
    val token: String,
    val ssid: String = "",
    val pass: String = "",
    val host: String = ""
)

fun parseMoDiQr(content: String): MoDiQrCode? {
    if (!content.startsWith(PREFIX, ignoreCase = true)) return null
    val params = content.substring(PREFIX.length)
        .split('&')
        .mapNotNull { item ->
            val separator = item.indexOf('=')
            if (separator <= 0) return@mapNotNull null
            decode(item.substring(0, separator)) to decode(item.substring(separator + 1))
        }
        .toMap()
    val transport = params["transport"]?.lowercase() ?: return null
    val token = params["token"]?.trim().orEmpty()
    val tokenBytes = token.toByteArray(StandardCharsets.US_ASCII)
    if (transport != "wifidirect" || token.isEmpty() || tokenBytes.size > 8 ||
        tokenBytes.toString(StandardCharsets.US_ASCII) != token
    ) {
        return null
    }
    return MoDiQrCode(
        transport = transport,
        deviceName = params["device"]?.trim().orEmpty(),
        token = token,
        ssid = params["ssid"].orEmpty(),
        pass = params["pass"].orEmpty(),
        host = params["host"].orEmpty()
    )
}

@Composable
fun P2pScannerScreen(
    onScanned: (MoDiQrCode) -> Unit,
    onDismiss: () -> Unit
) {
    val context = LocalContext.current
    val lifecycleOwner = LocalLifecycleOwner.current
    var hasScanned by remember { mutableStateOf(false) }
    var hintText by remember { mutableStateOf("将万能链路二维码对准摄像头") }
    val scanner = remember {
        BarcodeScanning.getClient(
            BarcodeScannerOptions.Builder().setBarcodeFormats(Barcode.FORMAT_QR_CODE).build()
        )
    }
    val analysisExecutor = remember { Executors.newSingleThreadExecutor() }
    val cameraProviderFuture = remember(context) { ProcessCameraProvider.getInstance(context) }

    DisposableEffect(cameraProviderFuture, scanner, analysisExecutor) {
        onDispose {
            runCatching { cameraProviderFuture.get().unbindAll() }
            analysisExecutor.shutdown()
            scanner.close()
        }
    }

    Column(
        modifier = Modifier.fillMaxSize(),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center
    ) {
        Text(hintText, style = MaterialTheme.typography.bodyMedium, modifier = Modifier.padding(16.dp))
        Box(modifier = Modifier.weight(1f).fillMaxWidth()) {
            AndroidView(
                factory = { PreviewView(it).apply { scaleType = PreviewView.ScaleType.FILL_CENTER } },
                modifier = Modifier.fillMaxSize(),
                update = { previewView ->
                    if (hasScanned) return@AndroidView
                    cameraProviderFuture.addListener({
                        runCatching {
                            val provider = cameraProviderFuture.get()
                            val preview = Preview.Builder().build().also {
                                it.surfaceProvider = previewView.surfaceProvider
                            }
                            val analysis = ImageAnalysis.Builder()
                                .setBackpressureStrategy(ImageAnalysis.STRATEGY_KEEP_ONLY_LATEST)
                                .build()
                            analysis.setAnalyzer(analysisExecutor) { imageProxy ->
                                processFrame(imageProxy, scanner) { raw ->
                                    if (hasScanned || raw == null) return@processFrame
                                    val parsed = parseMoDiQr(raw)
                                    if (parsed == null) {
                                        hintText = "二维码无效，请扫描万能链路配对码"
                                    } else {
                                        hasScanned = true
                                        onScanned(parsed)
                                    }
                                }
                            }
                            provider.unbindAll()
                            provider.bindToLifecycle(
                                lifecycleOwner,
                                CameraSelector.DEFAULT_BACK_CAMERA,
                                preview,
                                analysis
                            )
                        }.onFailure {
                            hintText = "相机启动失败，请检查权限后重试"
                            Log.e("P2pScanner", "Camera bind error: ${it.message}")
                        }
                    }, ContextCompat.getMainExecutor(context))
                }
            )
        }
        Button(
            onClick = onDismiss,
            modifier = Modifier.fillMaxWidth().padding(16.dp).height(48.dp)
        ) { Text("取消扫码", style = MaterialTheme.typography.labelLarge) }
    }
}

@SuppressLint("UnsafeOptInUsageError")
private fun processFrame(
    imageProxy: ImageProxy,
    scanner: BarcodeScanner,
    onResult: (String?) -> Unit
) {
    val mediaImage = imageProxy.image
    if (mediaImage == null) {
        imageProxy.close()
        return
    }
    scanner.process(InputImage.fromMediaImage(mediaImage, imageProxy.imageInfo.rotationDegrees))
        .addOnSuccessListener { barcodes -> onResult(barcodes.firstOrNull()?.rawValue) }
        .addOnFailureListener { onResult(null) }
        .addOnCompleteListener { imageProxy.close() }
}

private fun decode(value: String): String = runCatching {
    URLDecoder.decode(value, StandardCharsets.UTF_8.name())
}.getOrDefault(value)

private const val PREFIX = "MODI://"
