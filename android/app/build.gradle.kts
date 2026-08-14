plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.android")
    id("org.jetbrains.kotlin.plugin.compose")
}

// 发布签名凭据：keystore.properties 与 keystore/*.jks 均在 .gitignore 内，绝不入库。
// 纯 Kotlin stdlib 解析（该 DSL 脚本环境不解析 java.util.Properties）。
val keystoreProperties: Map<String, String> = rootProject.file("keystore.properties")
    .takeIf { it.exists() }
    ?.readLines()
    ?.mapNotNull { line ->
        val trimmed = line.trim()
        if (trimmed.isEmpty() || trimmed.startsWith("#")) null
        else trimmed.split("=", limit = 2).let { it[0].trim() to it.getOrElse(1) { "" }.trim() }
    }
    ?.toMap()
    ?: emptyMap()

val applicationRepositoryRoot = rootProject.projectDir.parentFile
val protocolArtifactVerifier = applicationRepositoryRoot.resolve("scripts/protocol/Verify-ProtocolArtifacts.ps1")
val fontArtifactVerifier = applicationRepositoryRoot.resolve("scripts/fonts/verify_fonts.py")
val generatedThirdPartyLegalResources = layout.buildDirectory.dir("generated/third-party-legal-resources")
val verifyProtocolArtifacts = tasks.register<Exec>("verifyProtocolArtifacts") {
    group = "verification"
    description = "Verifies the pinned Package B protocol binaries before any Android build."
    workingDir(applicationRepositoryRoot)
    commandLine(
        "pwsh",
        "-NoProfile",
        "-File",
        protocolArtifactVerifier.absolutePath,
        "-RepositoryRoot",
        applicationRepositoryRoot.absolutePath,
    )
    inputs.file(protocolArtifactVerifier)
    inputs.dir(applicationRepositoryRoot.resolve("third_party/modi-protocol"))
    outputs.upToDateWhen { false }
    doLast {
        val vendoredJar = applicationRepositoryRoot.resolve(
            "third_party/modi-protocol/maven/com/silvite/modi/modi-protocol-jvm/0.1.1/modi-protocol-jvm-0.1.1.jar",
        )
        val resolvedJar = configurations.getByName("debugRuntimeClasspath")
            .resolvedConfiguration
            .resolvedArtifacts
            .single {
                it.moduleVersion.id.group == "com.silvite.modi" &&
                    it.name == "modi-protocol-jvm" &&
                    it.moduleVersion.id.version == "0.1.1"
            }
            .file
        require(resolvedJar.readBytes().contentEquals(vendoredJar.readBytes())) {
            "Resolved protocol JAR differs from the canonical vendored Package B artifact: $resolvedJar"
        }
    }
}

val verifyFontArtifacts = tasks.register<Exec>("verifyFontArtifacts") {
    group = "verification"
    description = "Verifies the five locked cross-platform UI fonts before any Android build."
    workingDir(applicationRepositoryRoot)
    commandLine(
        providers.environmentVariable("PYTHON").orElse("python").get(),
        fontArtifactVerifier.absolutePath,
        "--repo-root",
        applicationRepositoryRoot.absolutePath,
    )
    inputs.files(
        fontArtifactVerifier,
        applicationRepositoryRoot.resolve("scripts/fonts/collect_characters.py"),
        applicationRepositoryRoot.resolve("scripts/fonts/font-sources.lock.json"),
        applicationRepositoryRoot.resolve("scripts/fonts/extra-characters.txt"),
        applicationRepositoryRoot.resolve("assets/fonts/font-artifacts.lock.json"),
    )
    inputs.dir(applicationRepositoryRoot.resolve("assets/fonts/android-res"))
    outputs.upToDateWhen { false }
}

val prepareThirdPartyLegalResources = tasks.register<Copy>("prepareThirdPartyLegalResources") {
    group = "build setup"
    description = "Stages the pinned Concentus license under a unique APK resource name."
    from(layout.projectDirectory.file("libs/concentus-1.0.1.LICENSE.txt")) {
        rename { "CONCENTUS-1.0.1-BSD-3-CLAUSE.txt" }
    }
    into(generatedThirdPartyLegalResources.map { it.dir("META-INF") })
}

tasks.matching { it.name == "preBuild" }.configureEach {
    dependsOn(verifyProtocolArtifacts)
    dependsOn(verifyFontArtifacts)
    dependsOn(prepareThirdPartyLegalResources)
}

android {
    namespace = "com.modi.connect"
    compileSdk = 36

    defaultConfig {
        applicationId = "com.modi.connect"
        minSdk = 29
        targetSdk = 36
        versionCode = 1
        versionName = "1.0.0"
    }

    // 发布签名：keystore 与密码均在 gitignore 内（keystore.properties / keystore/*.jks），绝不入库。
    signingConfigs {
        create("release") {
            storeFile = rootProject.file(keystoreProperties["storeFile"] ?: "")
            storePassword = keystoreProperties["storePassword"] ?: ""
            keyAlias = keystoreProperties["keyAlias"] ?: ""
            keyPassword = keystoreProperties["keyPassword"] ?: ""
        }
    }

    buildTypes {
        release {
            signingConfig = signingConfigs.getByName("release")
            isMinifyEnabled = false
            proguardFiles(
                getDefaultProguardFile("proguard-android-optimize.txt"),
                "proguard-rules.pro"
            )
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    kotlinOptions {
        jvmTarget = "17"
    }

    buildFeatures {
        compose = true
    }

    sourceSets.getByName("main").res.srcDir(
        applicationRepositoryRoot.resolve("assets/fonts/android-res"),
    )
    sourceSets.getByName("main").resources.srcDir(generatedThirdPartyLegalResources)
}

dependencies {
    // Package B: verified binary from the repository-local exclusive Maven source.
    implementation("com.silvite.modi:modi-protocol-jvm:0.1.1")

    // Compose BOM
    val composeBom = platform("androidx.compose:compose-bom:2024.12.01")
    implementation(composeBom)

    // Compose UI
    implementation("androidx.compose.ui:ui")
    implementation("androidx.compose.ui:ui-graphics")
    implementation("androidx.compose.ui:ui-tooling-preview")
    implementation("androidx.compose.material3:material3")
    implementation("androidx.compose.material:material-icons-extended")

    // Activity + Compose 集成
    implementation("androidx.activity:activity-compose:1.9.3")

    // Lifecycle
    implementation("androidx.lifecycle:lifecycle-runtime-ktx:2.8.7")
    implementation("androidx.lifecycle:lifecycle-runtime-compose:2.8.7")

    // Core
    implementation("androidx.core:core-ktx:1.15.0")

    // CameraX（扫码用）
    val cameraxVersion = "1.5.0"
    implementation("androidx.camera:camera-core:$cameraxVersion")
    implementation("androidx.camera:camera-camera2:$cameraxVersion")
    implementation("androidx.camera:camera-lifecycle:$cameraxVersion")
    implementation("androidx.camera:camera-view:$cameraxVersion")

    // MLKit Barcode Scanning（二维码解析）
    implementation("com.google.mlkit:barcode-scanning:17.3.0")

    // Concentus — 纯 Java Opus 编解码（本地 jar）
    implementation(files("libs/concentus-1.0.1.jar"))

    // Debug tooling
    testImplementation("junit:junit:4.13.2")
    debugImplementation("androidx.compose.ui:ui-tooling")
    debugImplementation("androidx.compose.ui:ui-test-manifest")
}
