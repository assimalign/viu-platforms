<#
.SYNOPSIS
    The single source of truth for which Viu Platforms projects produce NuGet packages, the
    canonical version they carry, and the CI package versions built toward it.

.DESCRIPTION
    scripts/Install-Local.ps1, scripts/Pack-Release.ps1, .github/workflows/release.yml, and
    .github/workflows/ci-packages.yml all pack through this inventory, so they cannot disagree about
    what ships. Get-ViuPlatformsReleasePackage carries the drift guard: a packable project under
    platforms/ that is not in the inventory fails the pack rather than silently shipping an
    incomplete set.

    The CI package functions keep two promises for .github/workflows/ci-packages.yml and
    .github/workflows/ci-packages-prune.yml: a CI build always sorts below the release it builds
    toward, and the pruner never selects a release version.
#>

Set-StrictMode -Version Latest

# Every published Viu Platforms package, in dependency-safe publication order. ApplicationModel has
# no project dependency; Hosting is the base of Markdown, Hosting.Browser, Routing, and Server;
# DevServer packs Server; the template pack is content only.
#
# RequiredEntry names the package paths a consumer loads by convention: the readme, MSBuild props
# and targets, analyzers, and tools. Assert-ViuPlatformsPackageLayout fails a pack that misplaces
# one, because NuGet still produces a package when a PackagePath is wrong, and the consumer then
# silently ignores the asset.
$script:ViuPlatformsReleasePackage = @(
    [pscustomobject]@{
        Id            = 'Assimalign.Cohesion.Viu.ApplicationModel'
        Project       = 'platforms/cohesion/Assimalign.Cohesion.Viu.ApplicationModel/src/Assimalign.Cohesion.Viu.ApplicationModel.csproj'
        RequiredEntry = @('README.md', 'lib/net10.0/Assimalign.Cohesion.Viu.ApplicationModel.dll')
    }
    [pscustomobject]@{
        Id            = 'Assimalign.Cohesion.Viu.Hosting'
        Project       = 'platforms/cohesion/Assimalign.Cohesion.Viu.Hosting/src/Assimalign.Cohesion.Viu.Hosting.csproj'
        RequiredEntry = @('README.md', 'lib/net10.0/Assimalign.Cohesion.Viu.Hosting.dll')
    }
    [pscustomobject]@{
        Id            = 'Assimalign.Cohesion.Viu.Markdown'
        Project       = 'platforms/cohesion/Assimalign.Cohesion.Viu.Markdown/src/Assimalign.Cohesion.Viu.Markdown.csproj'
        RequiredEntry = @(
            'README.md',
            'lib/net10.0/Assimalign.Cohesion.Viu.Markdown.dll',
            'build/Assimalign.Cohesion.Viu.Markdown.props',
            'build/Assimalign.Cohesion.Viu.Markdown.targets',
            'analyzers/dotnet/cs/Assimalign.Cohesion.Viu.Markdown.Generator.dll')
    }
    [pscustomobject]@{
        Id            = 'Assimalign.Cohesion.Viu.Hosting.Browser'
        Project       = 'platforms/cohesion/Assimalign.Cohesion.Viu.Hosting.Browser/src/Assimalign.Cohesion.Viu.Hosting.Browser.csproj'
        RequiredEntry = @('README.md', 'lib/net10.0/Assimalign.Cohesion.Viu.Hosting.Browser.dll')
    }
    [pscustomobject]@{
        Id            = 'Assimalign.Cohesion.Viu.Routing'
        Project       = 'platforms/cohesion/Assimalign.Cohesion.Viu.Routing/src/Assimalign.Cohesion.Viu.Routing.csproj'
        RequiredEntry = @('README.md', 'lib/net10.0/Assimalign.Cohesion.Viu.Routing.dll')
    }
    [pscustomobject]@{
        Id            = 'Assimalign.Cohesion.Viu.Server'
        Project       = 'platforms/cohesion/Assimalign.Cohesion.Viu.Server/src/Assimalign.Cohesion.Viu.Server.csproj'
        RequiredEntry = @(
            'README.md',
            'lib/net10.0/Assimalign.Cohesion.Viu.Server.dll',
            'buildTransitive/Assimalign.Cohesion.Viu.Server.props',
            'buildTransitive/Assimalign.Cohesion.Viu.Server.targets')
    }
    [pscustomobject]@{
        Id            = 'Assimalign.Cohesion.Viu.DevServer'
        Project       = 'platforms/cohesion/Assimalign.Cohesion.Viu.DevServer/src/Assimalign.Cohesion.Viu.DevServer.csproj'
        RequiredEntry = @(
            'README.md',
            'build/Assimalign.Cohesion.Viu.DevServer.targets',
            'tools/Assimalign.Cohesion.Viu.DevServer.dll')
    }
    [pscustomobject]@{
        Id            = 'Assimalign.Cohesion.Viu.Templates'
        Project       = 'platforms/cohesion/Assimalign.Cohesion.Viu.Templates/Assimalign.Cohesion.Viu.Templates.csproj'
        RequiredEntry = @(
            'README.md',
            'content/viu-cohesion/.template.config/template.json',
            'content/viu-cohesion-web/.template.config/template.json')
    }
)

# Prerelease identifiers the CI channel reserves. A canonical ViuPlatformsVersion may not contain
# them, so a release can never be mistaken for a CI build, and the pruner can never select one.
$script:ViuPlatformsCiReservedIdentifier = @('local', 'ci', 'pr')

function Get-ViuPlatformsReleasePackage {
    <#
    .SYNOPSIS
        The published packages and their project files, in dependency-safe publication order,
        failing when the inventory and the repository disagree.

    .DESCRIPTION
        Every csproj under platforms/ (template content excluded) whose IsPackable is not literally
        false must be in the inventory, and every inventory project must exist. Reading the csproj
        directly keeps the guard free of an MSBuild evaluation; every project that opts out
        declares IsPackable literally.

    .PARAMETER RepositoryDirectory
        The repository root.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string] $RepositoryDirectory
    )

    $configured = foreach ($package in $script:ViuPlatformsReleasePackage) {
        $projectPath = [System.IO.Path]::GetFullPath((Join-Path $RepositoryDirectory $package.Project))
        if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
            throw "The release inventory names $($package.Id) at $($package.Project), which does not exist."
        }

        [pscustomobject]@{ Id = $package.Id; ProjectPath = $projectPath; RequiredEntry = $package.RequiredEntry }
    }

    $contentSegment = [System.IO.Path]::DirectorySeparatorChar + 'content' + [System.IO.Path]::DirectorySeparatorChar
    $discovered = @(
        Get-ChildItem -LiteralPath (Join-Path $RepositoryDirectory 'platforms') -Filter '*.csproj' -File -Recurse |
            Where-Object { -not $_.FullName.Contains($contentSegment) } |
            Where-Object {
                $project = [xml](Get-Content -LiteralPath $_.FullName -Raw)
                $isPackable = $project.SelectSingleNode('/Project/PropertyGroup/IsPackable')
                -not ($isPackable -and $isPackable.InnerText.Trim() -eq 'false')
            } |
            ForEach-Object { [System.IO.Path]::GetFullPath($_.FullName) }
    )

    $difference = @(Compare-Object (@($configured.ProjectPath) | Sort-Object) ($discovered | Sort-Object))
    if ($difference.Count -ne 0) {
        throw "The Viu Platforms release inventory is incomplete: $($difference | Out-String)"
    }

    return $configured
}

function Assert-ViuPlatformsPackageLayout {
    <#
    .SYNOPSIS
        Fails unless a packed .nupkg holds every required entry at its exact path and no entry
        path is malformed.

    .DESCRIPTION
        NuGet appends a file name to a directory-style PackagePath with Path.Combine. On Linux a
        backslash is not a separator, so PackagePath="build\" packs build\/X.props there while the
        same project packs build/X.props on Windows: the package still builds, and consumers never
        load the asset. Every entry path must therefore use forward slashes only, with no empty
        segment and no leading slash, and each required entry must match ordinally.

    .PARAMETER PackagePath
        The .nupkg to inspect.

    .PARAMETER RequiredEntry
        Package-relative paths that must be present, for example build/<id>.props.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string] $PackagePath,

        [AllowEmptyCollection()]
        [string[]] $RequiredEntry = @()
    )

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($PackagePath)
    try {
        $entry = @($archive.Entries | ForEach-Object { $_.FullName })
    }
    finally {
        $archive.Dispose()
    }

    $packageName = [System.IO.Path]::GetFileName($PackagePath)
    $malformed = @($entry | Where-Object { $_.Contains('\') -or $_.Contains('//') -or $_.StartsWith('/') })
    if ($malformed.Count -gt 0) {
        throw "$packageName has malformed entry paths (a backslash or empty segment in a PackagePath packs them where consumers never look): $($malformed -join ', ')"
    }

    $entrySet = [System.Collections.Generic.HashSet[string]]::new([string[]] $entry, [System.StringComparer]::Ordinal)
    $missing = @($RequiredEntry | Where-Object { -not $entrySet.Contains($_) })
    if ($missing.Count -gt 0) {
        throw "$packageName is missing required entries: $($missing -join ', ')"
    }
}

function Assert-ViuPlatformsPortableAssembly {
    <#
    .SYNOPSIS
        Fails unless every assembly a packed .nupkg carries outside runtimes/ runs on every host
        architecture.

    .DESCRIPTION
        NuGet selects by RID only under runtimes/<rid>/, so lib/, analyzers/, and tools/ assets
        load on whatever host the consumer has. An assembly compiled for one RID still packs and
        still works on the packing machine, then fails on another architecture with "The assembly
        architecture is not compatible with the current process architecture." The DevServer's
        tools/ payload shipped that way once, because the Linux x64 CI pack stamped it AMD64.

        Every .dll and .exe outside runtimes/ must therefore be a managed image with PE machine
        I386 and the ILONLY flag, without 32BITREQUIRED (an x86-only image) or ILLIBRARY (a
        ReadyToRun image, whose native code targets one architecture).

    .PARAMETER PackagePath
        The .nupkg to inspect.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string] $PackagePath
    )

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $packageName = [System.IO.Path]::GetFileName($PackagePath)
    $nonPortable = [System.Collections.Generic.List[string]]::new()
    $archive = [System.IO.Compression.ZipFile]::OpenRead($PackagePath)
    try {
        foreach ($entry in $archive.Entries) {
            $extension = [System.IO.Path]::GetExtension($entry.FullName)
            if ($extension -notin @('.dll', '.exe') -or $entry.FullName.StartsWith('runtimes/', [System.StringComparison]::OrdinalIgnoreCase)) {
                continue
            }

            # PEHeaders needs a seekable stream; a zip entry stream is not one.
            $image = [System.IO.MemoryStream]::new()
            $entryStream = $entry.Open()
            try {
                $entryStream.CopyTo($image)
            }
            finally {
                $entryStream.Dispose()
            }

            $image.Position = 0
            try {
                # The constructor, not PEReader.PEHeaders: PowerShell can turn a throwing property
                # getter into $null.
                $headers = [System.Reflection.PortableExecutable.PEHeaders]::new($image)
                $machine = $headers.CoffHeader.Machine
                if ($null -eq $headers.CorHeader) {
                    $nonPortable.Add("$($entry.FullName) (native $machine image)")
                    continue
                }

                $flags = $headers.CorHeader.Flags
                $reason = @()
                if ($machine -ne [System.Reflection.PortableExecutable.Machine]::I386) { $reason += "machine $machine" }
                if (-not $flags.HasFlag([System.Reflection.PortableExecutable.CorFlags]::ILOnly)) { $reason += 'not ILONLY' }
                if ($flags.HasFlag([System.Reflection.PortableExecutable.CorFlags]::Requires32Bit)) { $reason += '32BITREQUIRED' }
                if ($flags.HasFlag([System.Reflection.PortableExecutable.CorFlags]::ILLibrary)) { $reason += 'ReadyToRun' }
                if ($reason.Count -gt 0) {
                    $nonPortable.Add("$($entry.FullName) ($($reason -join ', '))")
                }
            }
            catch {
                # PowerShell wraps the constructor's exception, so match the original one.
                if ($_.Exception.GetBaseException() -isnot [System.BadImageFormatException]) {
                    throw
                }

                $nonPortable.Add("$($entry.FullName) (not a PE image)")
            }
            finally {
                $image.Dispose()
            }
        }
    }
    finally {
        $archive.Dispose()
    }

    if ($nonPortable.Count -gt 0) {
        throw "$packageName carries assemblies that do not load on every host architecture (expected PE machine I386 with ILONLY): $($nonPortable -join '; ')"
    }
}

function Get-ViuPlatformsReleasePackageId {
    <#
    .SYNOPSIS
        The package ids of every published Viu Platforms package, in publication order.
    #>
    [CmdletBinding()]
    param()

    return @($script:ViuPlatformsReleasePackage | ForEach-Object { $_.Id })
}

function Get-ViuPlatformsVersion {
    <#
    .SYNOPSIS
        The canonical ViuPlatformsVersion declared in build/Targets/Build.Version.props.

    .PARAMETER RepositoryDirectory
        The repository root.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string] $RepositoryDirectory
    )

    $propsPath = Join-Path $RepositoryDirectory 'build/Targets/Build.Version.props'
    [xml] $versionProps = Get-Content -LiteralPath $propsPath -Raw
    $prefix = $versionProps.SelectSingleNode('/Project/PropertyGroup/ViuPlatformsVersionPrefix')
    $suffix = $versionProps.SelectSingleNode('/Project/PropertyGroup/ViuPlatformsVersionSuffix')
    if ($null -eq $prefix -or [string]::IsNullOrWhiteSpace($prefix.InnerText)) {
        throw "ViuPlatformsVersionPrefix was not found in $propsPath."
    }

    if ($null -eq $suffix -or [string]::IsNullOrWhiteSpace($suffix.InnerText)) {
        return $prefix.InnerText.Trim()
    }

    return "$($prefix.InnerText.Trim())-$($suffix.InnerText.Trim())"
}

function Get-ViuPlatformsCiPackageVersion {
    <#
    .SYNOPSIS
        Derives the unique CI package version .github/workflows/ci-packages.yml publishes for one
        build of the canonical Viu Platforms version.

    .DESCRIPTION
        A CI version must sort BELOW the canonical version it builds toward. Appending to the
        canonical prerelease sorts above it: 10.0.0-preview.1.ci.7 is higher than
        10.0.0-preview.1, so every CI build on the feed would outrank the real release.

        So the CI version extends the canonical version's PREDECESSOR. The final prerelease
        identifier must be a positive integer, and it is decremented. The channel and build
        identifiers then follow:

            canonical 10.0.0-preview.1   ->  base 10.0.0-preview.0
            push to main, run 57         ->  10.0.0-preview.0.ci.57
            pull request 12, run 58      ->  10.0.0-preview.0.pr.12.58

        Numeric identifiers are Int32 because NuGet's VersionComparer parses them with
        Int32.TryParse and compares a larger value as text, which orders "9" above "10".

        A stable canonical version, or a prerelease that does not end in a positive integer, has no
        derivable predecessor and is rejected rather than given an invented one. The reserved local,
        ci, and pr identifiers may not appear in the canonical version.

    .PARAMETER Version
        The canonical version from Get-ViuPlatformsVersion.

    .PARAMETER BuildNumber
        A number unique to the build within its channel; ci-packages.yml passes github.run_number.

    .PARAMETER PullRequestNumber
        The pull request the build belongs to. Selects the pr channel; omitted for main.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string] $Version,

        [Parameter(Mandatory)]
        [ValidateRange(1, 2147483647)]
        [int] $BuildNumber,

        [ValidateRange(1, 2147483647)]
        [int] $PullRequestNumber
    )

    $versionPattern = '^(?<major>0|[1-9][0-9]*)\.(?<minor>0|[1-9][0-9]*)\.(?<patch>0|[1-9][0-9]*)(?:-(?<suffix>[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?$'
    $versionMatch = [System.Text.RegularExpressions.Regex]::Match(
        $Version,
        $versionPattern,
        [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
    if (-not $versionMatch.Success) {
        throw "Version '$Version' must be SemVer in the supported form MAJOR.MINOR.PATCH-PRERELEASE."
    }

    if (-not $versionMatch.Groups['suffix'].Success) {
        throw "Canonical version '$Version' is stable, so no CI version can sort between the previous release and it. Move ViuPlatformsVersionSuffix to a prerelease line before publishing CI packages."
    }

    $canonicalIdentifier = $versionMatch.Groups['suffix'].Value.Split('.')
    foreach ($identifier in $canonicalIdentifier) {
        if ($identifier -match '^[0-9]+$' -and $identifier -notmatch '^(0|[1-9][0-9]*)$') {
            throw "Version '$Version' contains a numeric prerelease identifier with a leading zero."
        }

        if ($script:ViuPlatformsCiReservedIdentifier -contains $identifier.ToLowerInvariant()) {
            throw "Canonical version '$Version' contains the reserved '$identifier' identifier."
        }
    }

    $lastIdentifier = $canonicalIdentifier[-1]
    $lastNumber = 0
    if ($lastIdentifier -notmatch '^[1-9][0-9]*$' -or
        -not [int]::TryParse($lastIdentifier, [ref] $lastNumber)) {
        throw "Canonical version '$Version' must end in a positive integer prerelease identifier (for example preview.1) so its CI builds can sort below it."
    }

    $versionPrefix = '{0}.{1}.{2}' -f
        $versionMatch.Groups['major'].Value,
        $versionMatch.Groups['minor'].Value,
        $versionMatch.Groups['patch'].Value

    $baseIdentifier = @($canonicalIdentifier | Select-Object -SkipLast 1) + @([string] ($lastNumber - 1))
    if ($PSBoundParameters.ContainsKey('PullRequestNumber')) {
        $channel = 'pr'
        $channelIdentifier = @('pr', [string] $PullRequestNumber, [string] $BuildNumber)
    }
    else {
        $channel = 'ci'
        $channelIdentifier = @('ci', [string] $BuildNumber)
    }

    $baseSuffix = $baseIdentifier -join '.'
    $versionSuffix = @($baseIdentifier + $channelIdentifier) -join '.'

    return [pscustomobject]@{
        CanonicalVersion = $Version
        BaseVersion      = "$versionPrefix-$baseSuffix"
        Version          = "$versionPrefix-$versionSuffix"
        VersionPrefix    = $versionPrefix
        VersionSuffix    = $versionSuffix
        Channel          = $channel
    }
}

function ConvertFrom-ViuPlatformsCiPackageVersion {
    <#
    .SYNOPSIS
        Parses a version Get-ViuPlatformsCiPackageVersion produced back into its parts, and returns
        nothing for every other version.

    .DESCRIPTION
        The gate .github/workflows/ci-packages-prune.yml applies before it deletes anything: a
        version is a CI build only if it has exactly the shape the derivation emits,

            <base>.ci.<build>                   main
            <base>.pr.<pull request>.<build>    pull request

        where <base> is MAJOR.MINOR.PATCH-<prerelease>, its last prerelease identifier is a number,
        no identifier in it is the reserved local, ci, or pr, and <build> and <pull request> are
        positive Int32 values without a leading zero. Release, stable, and malformed versions all
        return nothing, so the pruner can never select them. The labels compare case-insensitively,
        as NuGet compares prerelease labels.

    .PARAMETER Version
        A package version as the feed lists it.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [AllowEmptyString()]
        [string] $Version
    )

    $coreMatch = [System.Text.RegularExpressions.Regex]::Match(
        $Version,
        '^(?<prefix>(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*))-(?<suffix>.+)$',
        [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
    if (-not $coreMatch.Success) {
        return
    }

    $identifier = $coreMatch.Groups['suffix'].Value.Split('.')
    foreach ($part in $identifier) {
        if ($part -notmatch '^[0-9A-Za-z-]+$') {
            return
        }

        if ($part -match '^[0-9]+$' -and $part -notmatch '^(0|[1-9][0-9]*)$') {
            return
        }
    }

    $pullRequestNumber = $null
    if ($identifier.Count -ge 4 -and $identifier[-3] -ieq 'pr') {
        $channel = 'pr'
        $pullRequestText = $identifier[-2]
        $buildText = $identifier[-1]
        $baseIdentifier = @($identifier | Select-Object -SkipLast 3)
    }
    elseif ($identifier.Count -ge 3 -and $identifier[-2] -ieq 'ci') {
        $channel = 'ci'
        $pullRequestText = $null
        $buildText = $identifier[-1]
        $baseIdentifier = @($identifier | Select-Object -SkipLast 2)
    }
    else {
        return
    }

    $buildNumber = 0
    if ($buildText -notmatch '^[1-9][0-9]*$' -or -not [int]::TryParse($buildText, [ref] $buildNumber)) {
        return
    }

    if ($channel -eq 'pr') {
        $parsedPullRequest = 0
        if ($pullRequestText -notmatch '^[1-9][0-9]*$' -or
            -not [int]::TryParse($pullRequestText, [ref] $parsedPullRequest)) {
            return
        }

        $pullRequestNumber = $parsedPullRequest
    }

    $baseNumber = 0
    if ($baseIdentifier.Count -lt 1 -or
        $baseIdentifier[-1] -notmatch '^(0|[1-9][0-9]*)$' -or
        -not [int]::TryParse($baseIdentifier[-1], [ref] $baseNumber)) {
        return
    }

    foreach ($part in $baseIdentifier) {
        if ($script:ViuPlatformsCiReservedIdentifier -contains $part.ToLowerInvariant()) {
            return
        }
    }

    $versionPrefix = $coreMatch.Groups['prefix'].Value
    return [pscustomobject]@{
        Version           = $Version
        BaseVersion       = "$versionPrefix-$($baseIdentifier -join '.')"
        VersionPrefix     = $versionPrefix
        Channel           = $channel
        PullRequestNumber = $pullRequestNumber
        BuildNumber       = $buildNumber
    }
}

function Select-ViuPlatformsCiPackageVersionToPrune {
    <#
    .SYNOPSIS
        Selects the CI package versions .github/workflows/ci-packages-prune.yml deletes from GitHub
        Packages.

    .DESCRIPTION
        Two rules select a version, and nothing else does:

            pull request   every <base>.pr.<N>.<build> version whose N is in
                           ClosedPullRequestNumber, compared as an integer.
            main           for each base, the main builds outside the MainBuildCount newest ones.
                           Omit MainBuildCount to leave main builds alone.

        A version ConvertFrom-ViuPlatformsCiPackageVersion does not recognize is never selected,
        whatever the parameters say. Main builds are ranked as whole builds by their newest
        CreatedAt on any package, ties broken by the higher build number, so each kept build stays
        complete and a restart of the run counter does not delete fresh builds as old. The output
        is in deletion order: closed pull requests (by number, then build), then main builds oldest
        first, packages by name within a build.

    .PARAMETER PackageVersion
        Every listed version, each with PackageName, VersionId, Version, and CreatedAt.

    .PARAMETER ClosedPullRequestNumber
        Pull requests whose builds are deleted. Pass only numbers the GitHub API reported closed.

    .PARAMETER MainBuildCount
        How many main builds to keep per base.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [AllowEmptyCollection()]
        [object[]] $PackageVersion,

        [AllowEmptyCollection()]
        [int[]] $ClosedPullRequestNumber = @(),

        [ValidateRange(1, 2147483647)]
        [int] $MainBuildCount
    )

    $closedPullRequest = [System.Collections.Generic.HashSet[int]]::new()
    foreach ($number in $ClosedPullRequestNumber) {
        $null = $closedPullRequest.Add($number)
    }

    $parsed = foreach ($entry in $PackageVersion) {
        foreach ($property in @('PackageName', 'VersionId', 'Version')) {
            $value = $entry.PSObject.Properties[$property]
            if ($null -eq $value -or $null -eq $value.Value -or [string]::IsNullOrWhiteSpace([string] $value.Value)) {
                throw "Package version entry is missing '$property'."
            }
        }

        $ciVersion = ConvertFrom-ViuPlatformsCiPackageVersion -Version ([string] $entry.Version)
        if ($null -eq $ciVersion) {
            continue
        }

        [pscustomobject]@{
            Entry       = $entry
            CiVersion   = $ciVersion
            BaseKey     = $ciVersion.BaseVersion.ToLowerInvariant()
            PackageName = [string] $entry.PackageName
        }
    }
    $parsed = @($parsed)

    $selected = [System.Collections.Generic.List[object]]::new()

    $pullRequestCandidate = @(
        $parsed |
            Where-Object {
                $_.CiVersion.Channel -eq 'pr' -and
                $closedPullRequest.Contains([int] $_.CiVersion.PullRequestNumber)
            } |
            Sort-Object `
                @{ Expression = { $_.CiVersion.PullRequestNumber } },
                @{ Expression = { $_.CiVersion.BuildNumber } },
                @{ Expression = { $_.PackageName } }
    )
    foreach ($candidate in $pullRequestCandidate) {
        $selected.Add((New-ViuPlatformsCiPackagePruneEntry `
                -Candidate $candidate `
                -Reason "pull request #$($candidate.CiVersion.PullRequestNumber) is closed"))
    }

    if ($PSBoundParameters.ContainsKey('MainBuildCount')) {
        $mainVersion = @($parsed | Where-Object { $_.CiVersion.Channel -eq 'ci' })
        foreach ($baseGroup in @($mainVersion | Group-Object -Property BaseKey | Sort-Object Name)) {
            $build = foreach ($buildGroup in @($baseGroup.Group | Group-Object -Property { $_.CiVersion.BuildNumber })) {
                $publishedAt = foreach ($candidate in $buildGroup.Group) {
                    ConvertTo-ViuPlatformsPackageTimestamp -Value $candidate.Entry.CreatedAt -Version $candidate.Entry.Version
                }

                [pscustomobject]@{
                    BuildNumber = [int] $buildGroup.Group[0].CiVersion.BuildNumber
                    PublishedAt = ($publishedAt | Sort-Object -Descending | Select-Object -First 1)
                    Candidate   = $buildGroup.Group
                }
            }

            $newestFirst = @(
                $build |
                    Sort-Object `
                        @{ Expression = { $_.PublishedAt }; Descending = $true },
                        @{ Expression = { $_.BuildNumber }; Descending = $true }
            )
            if ($newestFirst.Count -le $MainBuildCount) {
                continue
            }

            $expired = @($newestFirst | Select-Object -Skip $MainBuildCount)
            [array]::Reverse($expired)
            foreach ($expiredBuild in $expired) {
                foreach ($candidate in @($expiredBuild.Candidate | Sort-Object PackageName)) {
                    $selected.Add((New-ViuPlatformsCiPackagePruneEntry `
                            -Candidate $candidate `
                            -Reason "main build $($expiredBuild.BuildNumber) of $($candidate.CiVersion.BaseVersion) is older than the newest $MainBuildCount"))
                }
            }
        }
    }

    return , $selected.ToArray()
}

function New-ViuPlatformsCiPackagePruneEntry {
    param(
        [Parameter(Mandatory)]
        [object] $Candidate,

        [Parameter(Mandatory)]
        [string] $Reason
    )

    return [pscustomobject]@{
        PackageName       = $Candidate.PackageName
        VersionId         = $Candidate.Entry.VersionId
        Version           = [string] $Candidate.Entry.Version
        CreatedAt         = $Candidate.Entry.CreatedAt
        Channel           = $Candidate.CiVersion.Channel
        BaseVersion       = $Candidate.CiVersion.BaseVersion
        PullRequestNumber = $Candidate.CiVersion.PullRequestNumber
        BuildNumber       = $Candidate.CiVersion.BuildNumber
        Reason            = $Reason
    }
}

function ConvertTo-ViuPlatformsPackageTimestamp {
    param(
        [AllowNull()]
        [object] $Value,

        [Parameter(Mandatory)]
        [string] $Version
    )

    # ConvertFrom-Json turns the API's ISO 8601 strings into DateTime values on PowerShell 7, so
    # accept those as well as the raw text.
    if ($Value -is [System.DateTimeOffset]) {
        return $Value.ToUniversalTime()
    }

    if ($Value -is [datetime]) {
        return ([System.DateTimeOffset] $Value.ToUniversalTime())
    }

    $parsed = [System.DateTimeOffset]::MinValue
    if ($Value -is [string] -and
        [System.DateTimeOffset]::TryParse(
            $Value,
            [System.Globalization.CultureInfo]::InvariantCulture,
            [System.Globalization.DateTimeStyles]::AssumeUniversal,
            [ref] $parsed)) {
        return $parsed.ToUniversalTime()
    }

    throw "Main build version '$Version' has no parseable CreatedAt, so it cannot be ranked for retention."
}

Export-ModuleMember -Function `
    Get-ViuPlatformsReleasePackage,
    Assert-ViuPlatformsPackageLayout,
    Assert-ViuPlatformsPortableAssembly,
    Get-ViuPlatformsReleasePackageId,
    Get-ViuPlatformsVersion,
    Get-ViuPlatformsCiPackageVersion,
    ConvertFrom-ViuPlatformsCiPackageVersion,
    Select-ViuPlatformsCiPackageVersionToPrune
