package com.modi.connect.ui.runtime

import android.content.Context
import com.modi.connect.net.P2pPairStore

class RuntimeSettingsRepository(private val context: Context) {
    fun hasP2pPairing(): Boolean = P2pPairStore.hasPaired(context)
    fun loadP2pPair(): Pair<String, String>? = P2pPairStore.load(context)?.let { it.token to it.deviceName }
    fun saveP2pPair(token: String, deviceName: String) = P2pPairStore.save(context, token, deviceName)
    fun clearP2pPair() = P2pPairStore.clear(context)
    fun clearUiPreferences() = context.getSharedPreferences(UI_PREFERENCES, Context.MODE_PRIVATE).edit().clear().apply()

    companion object { private const val UI_PREFERENCES = "modi_ui" }
}
