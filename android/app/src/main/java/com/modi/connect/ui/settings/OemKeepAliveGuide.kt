package com.modi.connect.ui.settings

sealed interface KeepAliveIntentSpec {
    data class Component(val packageName: String, val className: String) : KeepAliveIntentSpec
    data object ApplicationDetails : KeepAliveIntentSpec
}

object OemKeepAliveGuide {
    fun resolve(manufacturer: String): List<KeepAliveIntentSpec> {
        val name = manufacturer.trim().lowercase()
        val vendor = when {
            name.contains("xiaomi") || name.contains("redmi") -> listOf(
                KeepAliveIntentSpec.Component("com.miui.securitycenter", "com.miui.permcenter.autostart.AutoStartManagementActivity"),
                KeepAliveIntentSpec.Component("com.miui.powerkeeper", "com.miui.powerkeeper.ui.HiddenAppsConfigActivity"),
            )
            name.contains("huawei") -> listOf(
                KeepAliveIntentSpec.Component("com.huawei.systemmanager", "com.huawei.systemmanager.startupmgr.ui.StartupNormalAppListActivity"),
            )
            name.contains("honor") -> listOf(
                KeepAliveIntentSpec.Component("com.hihonor.systemmanager", "com.hihonor.systemmanager.startupmgr.ui.StartupNormalAppListActivity"),
            )
            name.contains("oppo") || name.contains("realme") -> listOf(
                KeepAliveIntentSpec.Component("com.coloros.safecenter", "com.coloros.safecenter.startupapp.StartupAppListActivity"),
                KeepAliveIntentSpec.Component("com.oplus.battery", "com.oplus.powermanager.fuelgaue.PowerUsageModelActivity"),
            )
            name.contains("vivo") || name.contains("iqoo") -> listOf(
                KeepAliveIntentSpec.Component("com.vivo.permissionmanager", "com.vivo.permissionmanager.activity.BgStartUpManagerActivity"),
            )
            name.contains("samsung") -> listOf(
                KeepAliveIntentSpec.Component("com.samsung.android.lool", "com.samsung.android.sm.ui.battery.BatteryActivity"),
            )
            else -> emptyList()
        }
        return vendor + KeepAliveIntentSpec.ApplicationDetails
    }
}
