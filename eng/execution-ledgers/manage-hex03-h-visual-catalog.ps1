param(
    [ValidateSet('Validate', 'Write', 'Check')]
    [string] $Mode = 'Validate',
    [string] $InputPath = 'eng/execution-ledgers/hex03-h-visual-catalog.json'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path

function Require([bool] $Condition, [string] $Code) {
    if (-not $Condition) { throw "Hex03HVisualCatalogInvalid:$Code" }
}

function Stable-Text([string] $Text) {
    return (($Text -replace "`r`n", "`n").TrimEnd() + "`n")
}

function Write-IfChanged([string] $Path, [string] $Content) {
    $directory = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $directory)) { New-Item -ItemType Directory -Path $directory -Force | Out-Null }
    $current = if (Test-Path -LiteralPath $Path) { Get-Content -LiteralPath $Path -Raw -Encoding UTF8 } else { $null }
    if ($current -cne $Content) { [IO.File]::WriteAllText($Path, $Content, [Text.UTF8Encoding]::new($false)) }
}

$inputFullPath = Join-Path $repositoryRoot $InputPath
Require (Test-Path -LiteralPath $inputFullPath -PathType Leaf) 'InputMissing'
$source = Get-Content -LiteralPath $inputFullPath -Raw -Encoding UTF8 | ConvertFrom-Json
Require ([string] $source.schemaVersion -eq 'mirror-hex03-h-visual-catalog.v1') 'SchemaVersion'
Require ([string] $source.revision -eq 'hex03-h-visual-catalog.r2') 'Revision'
Require ([string] $source.campaignPlanId -eq 'PLAN-STORY-HEX03-CAMPAIGN-001') 'Campaign'
Require ([string] $source.evidenceBoundary -eq 'PresentationE4Only') 'EvidenceBoundary'
Require (@($source.items).Count -eq 9) 'ItemCount'

$seen = @{}
$previousLevel = 0
foreach ($item in @($source.items)) {
    $id = [string] $item.hStableId
    Require (-not [string]::IsNullOrWhiteSpace($id)) 'StableIdMissing'
    Require (-not $seen.ContainsKey($id)) "Duplicate:$id"
    $seen[$id] = $true
    $level = [int](([string] $item.levelCode).Substring(1))
    Require ($level -ge $previousLevel) "HierarchyOrder:$id"
    $previousLevel = $level
    Require (@($item.linePlanIds).Count -gt 0) "LineBindingMissing:$id"
    if (-not [string]::IsNullOrWhiteSpace([string] $item.repoImageRef)) {
        $imagePath = Join-Path $repositoryRoot ([string] $item.repoImageRef)
        Require (Test-Path -LiteralPath $imagePath -PathType Leaf) "ImageMissing:$id"
        Require ((Get-FileHash -LiteralPath $imagePath -Algorithm SHA256).Hash -eq [string] $item.repoImageSha256) "ImageHashMismatch:$id"
    }
}

$projection = [ordered]@{
    schemaVersion = 'mirror-hex03-h-visual-catalog-index.v1'
    revision = [string] $source.revision
    campaignPlanId = [string] $source.campaignPlanId
    evidenceBoundary = [string] $source.evidenceBoundary
    summary = [ordered]@{
        total = @($source.items).Count
        portableImages = @($source.items | Where-Object { -not [string]::IsNullOrWhiteSpace([string] $_.repoImageRef) }).Count
        definitionOnly = @($source.items | Where-Object statusCode -eq 'DefinitionOnly').Count
        visualUndecided = @($source.items | Where-Object statusCode -eq 'VisualUndecided').Count
    }
    items = @($source.items)
}
$json = Stable-Text ($projection | ConvertTo-Json -Depth 30)

$builder = [Text.StringBuilder]::new()
[void] $builder.AppendLine('# 수뢰둔 H 시각 자료 생성 색인')
[void] $builder.AppendLine()
[void] $builder.AppendLine('> `eng/execution-ledgers/hex03-h-visual-catalog.json`에서 생성한다. PNG는 기획 검토용이며 Unity Runtime 자산이나 E5 증거가 아니다.')
[void] $builder.AppendLine()
[void] $builder.AppendLine('| 계층 | H 안정 ID | 상태 | 이미지 | 효 |')
[void] $builder.AppendLine('| --- | --- | --- | --- | --- |')
foreach ($item in @($source.items)) {
    $image = if ([string]::IsNullOrWhiteSpace([string] $item.repoImageRef)) { '없음' } else { "[$([IO.Path]::GetFileName([string] $item.repoImageRef))](../../$([string] $item.repoImageRef -replace '^docs/', ''))" }
    [void] $builder.AppendLine("| $($item.levelCode) | ``$($item.hStableId)`` | ``$($item.statusCode)`` | $image | $(@($item.linePlanIds) -join ', ') |")
}
[void] $builder.AppendLine()
[void] $builder.AppendLine('- 현행 채택 범위: H1 단품 시각 후보만 유지')
[void] $builder.AppendLine('- H2·H3: 기존 이미지는 이력으로만 보존하며 새 시안 승인 전까지 `VisualUndecided`')
[void] $builder.AppendLine('- 후속 경계: Prefab·Renderer·Collider·Bounds·지면 접지·WI 발현은 E5, Animation은 E6')
$markdown = Stable-Text $builder.ToString()

$jsonPath = Join-Path $repositoryRoot 'docs/AI/generated/hex03-h-visual-catalog.json'
$markdownPath = Join-Path $repositoryRoot 'docs/AI/generated/hex03-h-visual-catalog.md'
if ($Mode -eq 'Write') {
    Write-IfChanged $jsonPath $json
    Write-IfChanged $markdownPath $markdown
} elseif ($Mode -eq 'Check') {
    Require ((Test-Path -LiteralPath $jsonPath) -and (Get-Content -LiteralPath $jsonPath -Raw -Encoding UTF8) -ceq $json) 'JsonOutputStale'
    Require ((Test-Path -LiteralPath $markdownPath) -and (Get-Content -LiteralPath $markdownPath -Raw -Encoding UTF8) -ceq $markdown) 'MarkdownOutputStale'
}

$portableImageCount = @($source.items | Where-Object { -not [string]::IsNullOrWhiteSpace([string] $_.repoImageRef) }).Count
Write-Output "Hex03HVisualCatalog:$Mode`:OK:Items=9:PortableImages=$portableImageCount"
