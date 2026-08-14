package com.modi.connect.ui.theme

import androidx.compose.material3.Typography
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.Font
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.sp
import com.modi.connect.R

object MoDiFontFamilies {
    val title = FontFamily(Font(R.font.modi_title_alimama_dongfang_dakai, FontWeight.Normal))
    val function = FontFamily(Font(R.font.modi_function_lxgw_wenkai, FontWeight.Normal))
    val body = FontFamily(Font(R.font.modi_body_zhuque_fangsong, FontWeight.Normal))
    val annotation = FontFamily(Font(R.font.modi_annotation_genyo_mincho, FontWeight.Normal))
    val default = FontFamily(Font(R.font.modi_default_source_han_serif, FontWeight.Normal))
}

private val MaterialTypographyDefaults = Typography()

val MoDiTypography = Typography(
    displayLarge = TextStyle(
        fontFamily = MoDiFontFamilies.title,
        fontWeight = FontWeight.Medium,
        fontSize = 24.sp,
        lineHeight = 31.sp
    ),
    displaySmall = TextStyle(
        fontFamily = MoDiFontFamilies.title,
        fontWeight = FontWeight.Bold,
        fontSize = 28.sp,
        lineHeight = 36.sp
    ),
    headlineSmall = TextStyle(
        fontFamily = MoDiFontFamilies.title,
        fontWeight = FontWeight.SemiBold,
        fontSize = 20.sp,
        lineHeight = 26.sp
    ),
    bodyLarge = TextStyle(
        fontFamily = MoDiFontFamilies.body,
        fontWeight = FontWeight.Normal,
        fontSize = 15.sp,
        lineHeight = 24.sp
    ),
    titleMedium = TextStyle(
        fontFamily = MoDiFontFamilies.function,
        fontWeight = FontWeight.Medium,
        fontSize = 16.sp,
        lineHeight = 22.sp
    ),
    titleSmall = TextStyle(
        fontFamily = MoDiFontFamilies.function,
        fontWeight = FontWeight.Medium,
        fontSize = 14.sp,
        lineHeight = 20.sp
    ),
    bodyMedium = TextStyle(
        fontFamily = MoDiFontFamilies.body,
        fontWeight = FontWeight.Normal,
        fontSize = 14.sp,
        lineHeight = 22.sp
    ),
    bodySmall = TextStyle(
        fontFamily = MoDiFontFamilies.annotation,
        fontWeight = FontWeight.Normal,
        fontSize = 12.sp,
        lineHeight = 19.sp
    ),
    labelLarge = TextStyle(
        fontFamily = MoDiFontFamilies.function,
        fontWeight = FontWeight.Medium,
        fontSize = 14.sp,
        lineHeight = 20.sp
    ),
    labelMedium = TextStyle(
        fontFamily = MoDiFontFamilies.function,
        fontWeight = FontWeight.Medium,
        fontSize = 12.sp,
        lineHeight = 16.sp
    ),
    displayMedium = MaterialTypographyDefaults.displayMedium.copy(fontFamily = MoDiFontFamilies.default),
    headlineLarge = MaterialTypographyDefaults.headlineLarge.copy(fontFamily = MoDiFontFamilies.default),
    headlineMedium = MaterialTypographyDefaults.headlineMedium.copy(fontFamily = MoDiFontFamilies.default),
    titleLarge = MaterialTypographyDefaults.titleLarge.copy(fontFamily = MoDiFontFamilies.function),
    labelSmall = MaterialTypographyDefaults.labelSmall.copy(fontFamily = MoDiFontFamilies.function),
)
