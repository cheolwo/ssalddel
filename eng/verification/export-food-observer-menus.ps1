[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
# 로컬 공개 자료의 작은 사본만 읽는다. 비밀번호는 컨테이너 내부 환경에서만 참조한다.
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$running = @(& docker ps --filter 'label=com.docker.compose.project=ssalddel-food-observer' --filter 'label=com.docker.compose.service=app' --format '{{.ID}}')
if ($LASTEXITCODE -or $running.Count) { throw 'Stop the isolated observer before replacing its frozen menu input.' }
$info = (& docker inspect --format '{{json .Config.Labels}}' hongdal-mysql-1 | ConvertFrom-Json)
if ($LASTEXITCODE -or $info.'com.docker.compose.project' -ne 'hongdal' -or $info.'com.docker.compose.service' -ne 'mysql' -or
    [IO.Path]::GetFullPath($info.'com.docker.compose.project.working_dir') -ne $repoRoot) { throw 'Public source container mismatch.' }
$sql = @'
SET TRANSACTION READ ONLY;
START TRANSACTION WITH CONSISTENT SNAPSHOT;
SELECT JSON_OBJECT('schemaVersion','food-observer-menus.r1','frozenAtUtc',DATE_FORMAT(UTC_TIMESTAMP(),'%Y-%m-%dT%H:%i:%sZ'),
 'menus',(SELECT JSON_ARRAYAGG(JSON_OBJECT('recipeId',v.Id,'title',v.Title,'sourceKey',s.SourceKey,
 'checksum',v.ContentChecksum,'observedAtUtc',DATE_FORMAT(v.LastCollectedAtUtc,'%Y-%m-%dT%H:%i:%sZ'),
 'sourceUrl',s.DocumentationUrl,'license',v.LicenseCodeAtCollection,'ingredient','양파',
 'ingredientId',18,'mappingStatus',(SELECT CONCAT(m.MappingState,' / ',m.MatchQualityCode,' / 내부 참고')
 FROM food_official_ingredient_price_mappings m WHERE m.IngredientId=18 AND m.SourceKey='kamis-price-observations'
 AND m.ExternalItemCode='245' AND m.IsActive=1 AND m.MatchQualityCode='ExactCommodity' ORDER BY m.Id LIMIT 1),
 'reference', (SELECT JSON_OBJECT('recordId',p.Id,'itemCode',p.ItemCode,'date',DATE_FORMAT(p.SurveyDate,'%Y-%m-%d'),
 'priceKrw',p.PriceKrw,'unit',p.Unit,'region',p.CountryName,'sourceUrl',p.SourceUrl)
 FROM agri_kamis_price_observations p WHERE p.ItemCode='245' AND p.IsPriceMissing=0 AND p.PriceKrw>0
 ORDER BY p.SurveyDate DESC,p.Id ASC LIMIT 1)))
 FROM food_official_recipe_variants v JOIN food_official_recipe_sources s ON s.Id=v.SourceId
 WHERE v.Id IN (3,5,7) AND s.SourceKey='mfds-cookrcp01' AND v.IsRemovedAtSource=0
 AND v.LicenseCodeAtCollection='공공데이터포털 이용허락범위 제한 없음'
 AND EXISTS (SELECT 1 FROM food_official_recipe_ingredients r WHERE r.RecipeVariantId=v.Id AND r.IngredientId=18 AND r.RequiresReview=0)
 AND EXISTS (SELECT 1 FROM food_official_ingredient_price_mappings m WHERE m.IngredientId=18 AND m.SourceKey='kamis-price-observations'
 AND m.ExternalItemCode='245' AND m.IsActive=1 AND m.MatchQualityCode='ExactCommodity')));
COMMIT;
'@
$raw = $sql | & docker exec -i hongdal-mysql-1 sh -c 'test "$MYSQL_DATABASE" = hongdal_dev && test "$MYSQL_USER" != root && export MYSQL_PWD="$MYSQL_PASSWORD" && exec mysql -u"$MYSQL_USER" hongdal_dev --batch --raw --skip-column-names --default-character-set=utf8mb4'
if ($LASTEXITCODE) { throw 'Read-only public menu export failed; no fallback.' }
$data = ($raw -join "`n") | ConvertFrom-Json
if ($data.schemaVersion -ne 'food-observer-menus.r1' -or $data.menus.Count -ne 3 -or
    @($data.menus | Where-Object { -not $_.reference -or $_.checksum.Length -ne 64 }).Count) { throw 'Menu evidence incomplete; export withheld.' }
$target = Join-Path $repoRoot 'artifacts/local/verification/food-observer/menus.json'
New-Item -ItemType Directory -Path (Split-Path $target) -Force | Out-Null
# 동결 파일은 실행 전에만 교체하며 Compose에서는 읽기 전용으로 마운트한다.
$content = $data | ConvertTo-Json -Depth 10
$bytes = [Text.UTF8Encoding]::new($false).GetBytes($content)
$fingerprint = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))
$archive = Join-Path (Split-Path $target) 'menu-snapshots'
New-Item -ItemType Directory -Path $archive -Force | Out-Null
$archived = Join-Path $archive ($fingerprint + '.json')
if (-not (Test-Path -LiteralPath $archived)) { [IO.File]::WriteAllBytes($archived,$bytes) }
[IO.File]::WriteAllBytes($target,$bytes)
Write-Output "Exported 3 menu names with existing source/ingredient/price references. Public database writes: 0. $target"
