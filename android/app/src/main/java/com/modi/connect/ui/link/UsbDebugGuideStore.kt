package com.modi.connect.ui.link

import android.content.Context

interface UsbDebugGuidePersistence {
    var hasBeenShown: Boolean
}

class InMemoryUsbDebugGuidePersistence : UsbDebugGuidePersistence {
    override var hasBeenShown: Boolean = false
}

class SharedPreferencesUsbDebugGuidePersistence(context: Context) : UsbDebugGuidePersistence {
    private val preferences = context.getSharedPreferences(PREFERENCES, Context.MODE_PRIVATE)

    override var hasBeenShown: Boolean
        get() = preferences.getBoolean(KEY_SHOWN, false)
        set(value) { preferences.edit().putBoolean(KEY_SHOWN, value).apply() }

    private companion object {
        const val PREFERENCES = "modi_usb_debug_guide_v1"
        const val KEY_SHOWN = "shown"
    }
}

class UsbDebugGuideStore(private val persistence: UsbDebugGuidePersistence) {
    fun shouldShowForUsbSelection(): Boolean = !persistence.hasBeenShown
    fun markShown() { persistence.hasBeenShown = true }
}
