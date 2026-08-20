package com.modi.connect.ui.onboarding

data class OnboardingState(
    val stepIndex: Int = 0,
    val explanation: String? = null,
) {
    fun next(): OnboardingState = copy(stepIndex = (stepIndex + 1).coerceAtMost(LAST_STEP), explanation = null)
    fun back(): OnboardingState = copy(stepIndex = (stepIndex - 1).coerceAtLeast(0), explanation = null)
    fun permissionDenied(permissionName: String): OnboardingState =
        copy(explanation = "$permissionName 权限未授予。你可以继续阅读说明，之后再从推流页或系统设置授权。")

    companion object {
        const val STEP_COUNT = 4
        const val LAST_STEP = STEP_COUNT - 1
    }
}
