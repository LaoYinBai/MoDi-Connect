package com.modi.connect.ui.profile

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.HelpOutline
import androidx.compose.material.icons.automirrored.outlined.KeyboardArrowRight
import androidx.compose.material.icons.automirrored.outlined.MenuBook
import androidx.compose.material.icons.outlined.FavoriteBorder
import androidx.compose.material.icons.outlined.Settings
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.unit.dp

@Composable
fun ProfileScreen(
    onStory: () -> Unit,
    onSponsors: () -> Unit,
    onSupport: () -> Unit,
    onSettings: () -> Unit,
    modifier: Modifier = Modifier
) {
    Column(modifier.fillMaxSize()) {
        Text(
            text = "我的",
            style = MaterialTheme.typography.displaySmall,
            modifier = Modifier.padding(horizontal = 24.dp, vertical = 20.dp)
        )
        ProfileGroup(
            items = listOf(
                ProfileItem("故事汇", Icons.AutoMirrored.Outlined.MenuBook, onStory),
                ProfileItem("赞助榜", Icons.Outlined.FavoriteBorder, onSponsors),
                ProfileItem("技术支持", Icons.AutoMirrored.Outlined.HelpOutline, onSupport)
            )
        )
        Spacer(Modifier.height(24.dp))
        ProfileGroup(items = listOf(ProfileItem("设置", Icons.Outlined.Settings, onSettings)))
    }
}

private data class ProfileItem(val label: String, val icon: ImageVector, val onClick: () -> Unit)

@Composable
private fun ProfileGroup(items: List<ProfileItem>) {
    Column(Modifier.padding(horizontal = 16.dp)) {
        items.forEachIndexed { index, item ->
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .height(56.dp)
                    .clip(RoundedCornerShape(28.dp))
                    .clickable(role = Role.Button, onClick = item.onClick)
                    .padding(horizontal = 16.dp),
                verticalAlignment = Alignment.CenterVertically
            ) {
                Icon(item.icon, contentDescription = null, modifier = Modifier.size(24.dp), tint = MaterialTheme.colorScheme.onSurfaceVariant)
                Text(item.label, style = MaterialTheme.typography.titleSmall, modifier = Modifier.weight(1f).padding(start = 16.dp))
                Icon(Icons.AutoMirrored.Outlined.KeyboardArrowRight, contentDescription = null, tint = MaterialTheme.colorScheme.onSurfaceVariant)
            }
            if (index < items.lastIndex) {
                HorizontalDivider(modifier = Modifier.padding(start = 16.dp), color = MaterialTheme.colorScheme.outlineVariant)
            }
        }
    }
}
