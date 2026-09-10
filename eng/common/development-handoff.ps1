# 인계는 기존 원장의 조회 결과다. 이 모듈은 Goal·승인·증거를 변경하지 않는다.
. (Join-Path $PSScriptRoot 'parallel-development-work.ps1')

function Get-HandoffHash([object] $Value) {
    $bytes = [Text.Encoding]::UTF8.GetBytes(($Value | ConvertTo-Json -Depth 90 -Compress))
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($algorithm.ComputeHash($bytes)).Replace('-', '') }
    finally { $algorithm.Dispose() }
}

function Read-HandoffJson([string] $Root, [string] $Path) {
    Get-Content -LiteralPath (Resolve-ParallelWorkPath $Root $Path) -Raw -Encoding UTF8 | ConvertFrom-Json
}

function Resolve-HandoffSelection($Policy, $Ledger, [string] $GoalId, [string] $WorkItemId) {
    $goals = @(Get-ParallelWorkField $Policy 'nativeInteractionGoals' @() | Where-Object goalStableId -eq $GoalId)
    $legacy = $false
    if ($goals.Count -eq 0) {
        $legacy = $true
        $goals = @($Ledger.items | Where-Object {
            $context = $_.loopStableId -replace '^playable-loop:', '' -replace '\.v[0-9]+$', ''
            $wi = $_.nextWorldInteractionId.ToLowerInvariant().Replace('_', '-')
            $GoalId -eq "interaction-goal:$context.$wi.v1"
        })
    }
    if ($goals.Count -ne 1) { throw "DevelopmentHandoffInvalid:GoalSelection:$GoalId" }
    $goal = $goals[0]
    $wi = if ($legacy) { $goal.nextWorldInteractionId } else { $goal.worldInteractionId }
    $candidates = @(Get-ParallelDevelopmentWorkItems $Ledger | Where-Object {
        $_.worldInteractionId -eq $wi -and (($legacy -and $_.loopStableId -eq $goal.loopStableId) -or
            ((-not $legacy) -and $_.workOrderRef -eq $goal.workOrderRef))
    })
    $work = $null
    if ($WorkItemId) {
        $chosen = @($candidates | Where-Object workItemId -eq $WorkItemId)
        if ($chosen.Count -ne 1) { throw "DevelopmentHandoffInvalid:WorkItemSelection:$WorkItemId" }
        $work = $chosen[0]
    } elseif ($candidates.Count -eq 1) { $work = $candidates[0] }
    elseif ($candidates.Count -gt 1 -or $legacy) { throw 'DevelopmentHandoffInvalid:WorkItemIdRequired' }
    [pscustomobject]@{ goal=$goal; work=$work; legacy=$legacy; wi=$wi }
}

function Resolve-HandoffFile([string] $Root, [string] $UnityRoot, [string] $Reference) {
    # 새 인계 설명의 Unity 참조는 unity: 접두사와 저장소 상대 경로를 사용한다.
    if ($Reference.StartsWith('unity:')) {
        if (-not $UnityRoot -or -not (Test-Path -LiteralPath $UnityRoot -PathType Container)) {
            throw 'DevelopmentHandoffInvalid:UnityRepositoryMissing'
        }
        $relative = $Reference.Substring(6)
        return [pscustomobject]@{ repository='unity'; path=$relative.Replace('\','/'); fullPath=(Resolve-ParallelWorkPath $UnityRoot $relative) }
    }
    if ([IO.Path]::IsPathRooted($Reference)) {
        if (-not $UnityRoot) { throw 'DevelopmentHandoffInvalid:UnityRepositoryMissing' }
        $relative = [IO.Path]::GetRelativePath([IO.Path]::GetFullPath($UnityRoot), [IO.Path]::GetFullPath($Reference))
        return Resolve-HandoffFile $Root $UnityRoot "unity:$relative"
    }
    [pscustomobject]@{ repository='hongdal'; path=$Reference.Replace('\','/'); fullPath=(Resolve-ParallelWorkPath $Root $Reference) }
}

function Test-HandoffOverlap([string] $Left, [string] $Right) {
    $a = $Left.TrimEnd('\','/').Replace('\','/')
    $b = $Right.TrimEnd('\','/').Replace('\','/')
    $a.Equals($b, [StringComparison]::OrdinalIgnoreCase) -or
        $a.StartsWith($b+'/', [StringComparison]::OrdinalIgnoreCase) -or
        $b.StartsWith($a+'/', [StringComparison]::OrdinalIgnoreCase)
}

function Assert-HandoffPhysicalPath([string] $Path) {
    $cursor = $Path
    while ($cursor) {
        if ((Test-Path -LiteralPath $cursor) -and ((Get-Item -LiteralPath $cursor).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw "DevelopmentHandoffInvalid:ReparsePoint:$Path"
        }
        $parent = Split-Path -Parent $cursor
        if (-not $parent -or $parent -eq $cursor) { break }; $cursor = $parent
    }
}

function Get-HandoffInventory([string] $Root, [string] $UnityRoot, [string[]] $References) {
    $result = @{}
    foreach ($reference in @($References | Sort-Object -Unique)) {
        if ([string]::IsNullOrWhiteSpace($reference)) { continue }
        $resolved = Resolve-HandoffFile $Root $UnityRoot $reference
        # reparse point를 통하여 승인 저장소 밖의 파일을 읽지 않는다.
        Assert-HandoffPhysicalPath $resolved.fullPath
        $files = @($resolved.fullPath)
        if (Test-Path -LiteralPath $resolved.fullPath -PathType Container) {
            $files = @(& rg --files --hidden -g '!.git' -g '!Library' -g '!Temp' -g '!bin' -g '!obj' -g '!artifacts' -g '!vendor' -g '!.vs' $resolved.fullPath)
            if ($LASTEXITCODE -gt 1) { throw "DevelopmentHandoffInvalid:InventoryFailed:$reference" }
        }
        foreach ($file in $files) {
            Assert-HandoffPhysicalPath $file
            $repoRoot = if ($resolved.repository -eq 'unity') { $UnityRoot } else { $Root }
            $path = [IO.Path]::GetRelativePath($repoRoot, $file).Replace('\','/')
            $key = "$($resolved.repository):$path"
            $hash = if (Test-Path -LiteralPath $file -PathType Leaf) { (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash } else { 'Missing' }
            $result[$key] = [ordered]@{ repository=$resolved.repository; path=$path; sha256=$hash }
        }
    }
    @($result.Keys | Sort-Object | ForEach-Object { $result[$_] })
}

function Test-HandoffBaseline($Previous, $Current) {
    $issues = [Collections.Generic.List[string]]::new()
    if ((Get-ParallelWorkField $Previous 'schemaVersion') -ne 'development-handoff.v1') { $issues.Add('BaselineSchemaInvalid') }
    foreach ($name in @('goalId','workItemId','selectionHash')) {
        if ((Get-ParallelWorkField $Previous $name) -cne (Get-ParallelWorkField $Current $name)) { $issues.Add("BaselineChanged:$name") }
    }
    $before = @{}; $after = @{}
    foreach ($file in @($Previous.files)) { $before["$($file.repository):$($file.path)"] = $file.sha256 }
    foreach ($file in @($Current.files)) { $after["$($file.repository):$($file.path)"] = $file.sha256 }
    foreach ($key in @(@($before.Keys)+@($after.Keys) | Sort-Object -Unique)) {
        if ($before[$key] -cne $after[$key]) { $issues.Add("InputChanged:$key") }
    }
    $issues.ToArray()
}

function Test-HandoffDescription($Notes) {
    foreach ($field in @('purpose','excludedScope','firstTask','completionCriteria','decisionBoundary','returnInstructions')) {
        if ([string]::IsNullOrWhiteSpace([string](Get-ParallelWorkField $Notes $field))) { "DescriptionMissing:$field" }
    }
    foreach ($field in @('moduleRelations','readRefs','validationCommands')) {
        $values = @(Get-ParallelWorkField $Notes $field @())
        if ($values.Count -eq 0 -or @($values | Where-Object { [string]::IsNullOrWhiteSpace([string]$_) }).Count -gt 0) { "DescriptionMissing:$field" }
    }
}

function Test-HandoffResearch([string] $Root, $Order) {
    if ($null -eq $Order.PSObject.Properties['requiredResearch']) { 'ResearchDeclarationMissing'; return }
    foreach ($binding in @($Order.requiredResearch)) {
        if ($null -eq $binding -or $binding -is [string]) { 'ResearchBindingNeedsReview'; continue }
        if ((Get-ParallelWorkField $binding 'statusCode') -ne 'Accepted') { 'RequiredResearchNotAccepted' }
        $reference = [string](Get-ParallelWorkField $binding 'documentRef')
        $hash = [string](Get-ParallelWorkField $binding 'sha256')
        if (-not $reference -or $hash -notmatch '^[A-Fa-f0-9]{64}$') { 'ResearchBindingNeedsReview'; continue }
        try {
            $path = Resolve-ParallelWorkPath $Root $reference
            if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $hash) { 'ResearchHashMismatch' }
        } catch { 'ResearchDocumentMissing' }
    }
}

function ConvertTo-HandoffMarkdown($Packet) {
    $lines = [Collections.Generic.List[string]]::new()
    $lines.Add("# 제작 인계 — $($Packet.goalId)")
    $lines.Add(''); $lines.Add('> 원본 명세에서 생성한 조회 요약. 실행 승인·자동 활성화·Evidence 승격이 아니다.')
    $lines.Add(''); $lines.Add("- 점검: $($Packet.statusCode) / WI: $($Packet.worldInteractionId) / 승인 상한: $($Packet.deliveryCap)")
    $ownerLabel = if ($Packet.ownerThreadId) { $Packet.ownerThreadId } else { '미지정' }
    $workLabel = if ($Packet.workItemId) { $Packet.workItemId } else { '별도 work item 없음' }
    $lines.Add("- 담당: $ownerLabel / 작업: $workLabel")
    $lines.Add("- 기획: $($Packet.designDocumentRef) / 작업 명세: $($Packet.workOrderRef)")
    foreach ($section in @(
        @('목적','purpose'), @('제외 범위','excludedScope'), @('모듈 연결 — 기존·수정·신규·미연결','moduleRelations'),
        @('먼저 읽을 파일','readRefs'), @('첫 작업','firstTask'), @('완료 조건','completionCriteria'),
        @('검증 명령 — 자동 실행하지 않음','validationCommands'), @('판단·중단 경계','decisionBoundary'), @('결과 반환','returnInstructions'))) {
        $lines.Add(''); $lines.Add("## $($section[0])"); $lines.Add('')
        foreach ($value in @(Get-ParallelWorkField $Packet.notes $section[1] @('미작성'))) { $lines.Add("- $value") }
    }
    $lines.Add(''); $lines.Add('## 수정 범위 — 저장소별'); $lines.Add('')
    foreach ($path in $Packet.writePaths) { $lines.Add("- $path") }
    $lines.Add(''); $lines.Add('## 점검 결과'); $lines.Add('')
    foreach ($code in $Packet.blockerCodes) { $lines.Add("- 차단: $code") }
    foreach ($code in $Packet.warningCodes) { $lines.Add("- 경고: $code") }
    $lines.Add(''); $lines.Add('기준 파일 hash와 저장소 HEAD는 함께 생성된 JSON에서 확인한다. 시험·실행·화면 증거는 원문 반환 기록을 따른다.')
    ($lines -join "`n") + "`n"
}
