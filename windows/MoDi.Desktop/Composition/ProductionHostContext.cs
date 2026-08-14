using System;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;

namespace MoDi.Desktop.Composition;

public sealed record ProductionHostContext(
    Func<IStorageProvider?> StorageProviderAccessor,
    Func<IClipboard?> ClipboardAccessor,
    string? CommunityWebsiteUrl);
