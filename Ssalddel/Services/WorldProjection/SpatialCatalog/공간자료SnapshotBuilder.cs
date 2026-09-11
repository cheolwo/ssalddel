using MongoDB.Bson;
using Ssalddel.Contracts.Common.WorldProjection;

namespace Ssalddel.Services.WorldProjection.SpatialCatalog;

public sealed record 공간자료Source(string SourcePath, string Dataset, string Kind, byte[] Bytes,
    string? AreaStableId = null);
public sealed record 공간자료ImportBatch(BsonDocument Snapshot, IReadOnlyList<BsonDocument> Documents,
    IReadOnlyList<BsonDocument> Elements, IReadOnlyList<BsonDocument> Relations);

// 스키마를 확인한 읽기 전용 추출기다. 파일·DB·게임 상태를 변경하거나 Ref의 의미를 추정하지 않는다.
public static class 공간자료SnapshotBuilder
{
    private static readonly HashSet<string> Schemas = new(StringComparer.Ordinal)
    {
        "mirror-graph-map-code-binding-catalog.v1", "simulation-world-graph-map.v1",
        "mirror-graph-map-placement-rule-catalog.v1", "mirror-graph-map-plan.v3",
        "mirror-graph-map-partition-catalog.v1", "mirror-graph-map-normalization.v1",
        "ssalddel-focused-graph-map.v1", "mirror-graph-map-overlay-catalog.v1",
        "simulation-world-placement-map-preparation-profile.v1", "simulation-world-placement-map-e5-profile.v1",
        "simulation-world-h4-e4-preparation-profile.v1", "simulation-world-h1-placement-rule.v1",
        "simulation-world-regional-reference-sources.v1", "simulation-reality-context-catalog.v1",
        "myeonmok-address-spatial-index.r1", "myeonmok-diorama-presentation.r1",
        "myeonmok-game-object-catalog.v1", "myeonmok-game-object-candidate-projection.v1",
        "five-element-work-object-catalog.v1", "neighborhood-game-object-candidate-projection.v1",
        "neighborhood-spatial-package.v1", "neighborhood-spatial-package.v2",
        "neighborhood-package-registry.v1", "neighborhood-package-registry.v2",
        "neighborhood-observation-inventory.v1", "spatial-semantic-layer-catalog.v1",
        "neighborhood-unity-projection-handoff.v1"
    };

    private static readonly Dictionary<string, string> ArrayKinds = new(StringComparer.Ordinal)
    {
        ["nodes"]="GraphNode", ["edges"]="GraphEdge", ["constraints"]="Constraint",
        ["governanceOnlyConstraints"]="Constraint", ["areaRuleProfiles"]="AreaRule",
        ["bindings"]="CodeBinding", ["bindingAssignments"]="BindingAssignment", ["unboundTargets"]="UnboundTarget",
        ["subgraphs"]="Subgraph", ["connectors"]="Connector", ["ports"]="Port",
        ["elements"]="NormalizedElement", ["relations"]="NormalizedRelation", ["lineBindings"]="LineBinding",
        ["layers"]="Layer", ["overlays"]="Overlay", ["traversalProfiles"]="Traversal",
        ["instances"]="Placement", ["requiredPlacementInstances"]="PlacementIntent", ["anchors"]="Anchor",
        ["paths"]="Path", ["actors"]="Actor", ["features"]="Feature", ["sourceLayers"]="SourceLayer",
        ["tileSummaries"]="TileSummary", ["scenarioOverlays"]="ScenarioOverlay", ["knownGaps"]="Gap",
        ["unplacedCatalog"]="UnplacedSummary", ["requiredH3Roles"]="Role", ["h3RoleBindings"]="RoleBinding",
        ["graphRouteBindings"]="RouteBinding", ["conditionalRouteRules"]="RouteRule",
        ["sourceSnapshots"]="SourceSnapshot", ["profiles"]="RealityProfile",
        ["addresses"]="Address", ["activityObservations"]="ActivityObservation", ["landSampleLinks"]="SampleLink",
        ["buildingOverlays"]="BuildingOverlay", ["buildings"]="Building", ["roads"]="Road",
        ["workflowBindings"]="WorkflowModuleBinding", ["candidateGenerationProfiles"]="CandidateGenerationProfile",
        ["archetypes"]="ObjectArchetype", ["interactionProfiles"]="InteractionProfile",
        ["roleActionBindings"]="RoleActionBinding", ["candidates"]="ObjectCandidateBinding",
        ["packages"]="NeighborhoodPackageRegistration", ["observations"]="ObservationCandidate",
        ["compatibilityAliases"]="CompatibilityAlias", ["coordinateFrames"]="CoordinateFrameRegistration",
        ["sourceInputs"]="SourceInput", ["semanticLayers"]="SemanticLayerDefinition",
        ["layerBindings"]="SemanticLayerBinding"
    };

    private static readonly string[] IdentityKeys =
    ["candidateStableId", "packageStableId", "observationStableId", "aliasStableId", "registryStableId", "inventoryStableId",
     "semanticLayerStableId", "layerBindingStableId",
     "bindingStableId", "objectArchetypeStableId",
     "nodeStableId", "nodeId", "edgeStableId", "edgeId", "placementInstanceId", "addressStableId",
     "observationId", "sampleId", "spatialId", "stableId", "constraintId", "bindingId", "subgraphId",
     "connectorId", "portId", "overlayId", "layerId", "layerCode", "actorId", "profileStableId",
     "sourceStableId", "relationRef", "elementRef", "tileRef", "routeCode", "roleCode", "linePlanId",
     "coordinateFrameStableId", "moduleCode", "id"];

    public static 공간자료ImportBatch Build(IReadOnlyList<공간자료Source> sources)
    {
        if (sources.Count is 0 or > 100 || sources.Select(x=>x.SourcePath).Distinct(StringComparer.Ordinal).Count()!=sources.Count)
            throw new InvalidDataException("SpatialSourceSetInvalid");
        var documents = new List<BsonDocument>(); var elements = new List<BsonDocument>(); var relations = new List<BsonDocument>();
        foreach (var source in sources.OrderBy(x=>x.SourcePath, StringComparer.Ordinal))
        {
            if (source.Bytes.Length > 8 * 1024 * 1024 || source.SourcePath.Contains("..", StringComparison.Ordinal))
                throw new InvalidDataException("SpatialInputBoundExceeded");
            if (source.AreaStableId is { } sourceArea &&
                (sourceArea.Length is 0 or > 256 || sourceArea.Any(char.IsControl)))
                throw new InvalidDataException("SpatialAreaStableIdInvalid");
            var payload = 공간자료Json.Parse(source.Bytes);
            var schema = 공간자료Json.Text(payload, "schemaVersion", "schema");
            if (schema.Length == 0)
            {
                // 원문에 schema 필드를 주입하지 않고 두 기존 형식을 어댑터 metadata로만 식별한다.
                schema = source.Kind switch
                {
                    "Geometry" when payload.Contains("buildings") && payload.Contains("roads") && payload.Contains("coordinateMethod") => "adapter:sagajeong-reference",
                    "PlacementMap" when source.SourcePath.EndsWith("neighborhood-market-extension.v1.json", StringComparison.Ordinal) && payload.Contains("baseProfileRef") && payload.Contains("features") => "adapter:neighborhood-market-extension",
                    _ => throw new InvalidDataException("SpatialSchemaMissing")
                };
            }
            else if (!Schemas.Contains(schema)) throw new InvalidDataException("SpatialSchemaUnsupported");
            var rawHash = 공간자료Json.Hash(source.Bytes);
            var payloadArea = 공간자료Json.Text(payload, "areaStableId");
            if (source.AreaStableId is not null && payloadArea.Length>0 && source.AreaStableId!=payloadArea)
                throw new InvalidDataException("SpatialAreaStableIdMismatch");
            var areaStableId = source.AreaStableId ?? payloadArea;
            var revision = payload.GetValue("revision", BsonNull.Value);
            var revisionKey = revision.IsBsonNull ? "content:" + rawHash : 공간자료Json.Element(revision, true).GetRawText();
            // 어댑터가 만드는 record 구조가 달라지면 같은 원본 판본도 별도 불변 문서다.
            // 이를 ID에 결속하지 않으면 이전 어댑터 기록과 새 recordHash가 충돌한다.
            var documentId = "doc:" + 공간자료Json.Hash(source.SourcePath + "\n" + schema + "\n" + revisionKey
                + "\n" + 공간자료CatalogCodes.AdapterVersion + "\n" + areaStableId);
            var stableId = DocumentStableId(source.Kind, schema, payload);
            var coordinates = CoordinateFrame(payload, source.Kind);
            var doc = new BsonDocument
            {
                ["_id"]=documentId, ["sourcePath"]=source.SourcePath, ["dataset"]=source.Dataset,
                ["areaStableId"]=areaStableId, ["kind"]=source.Kind,
                ["schema"]=schema, ["stableId"]=stableId, ["revision"]=revision.DeepClone(), ["revisionKey"]=revisionKey,
                ["revisionOrigin"]=revision.IsBsonNull ? "ContentHashFallback" : "SourceField",
                ["rawSha256"]=rawHash, ["sourceBytes"]=source.Bytes.LongLength,
                ["adapterVersion"]=공간자료CatalogCodes.AdapterVersion, ["reviewState"]="PendingHumanReview",
                ["sourceState"]=공간자료Json.Text(payload, "stateCode", "status", "qualityCode"),
                ["privateReviewOnly"]=true, ["distributionApproved"]=false, ["gameStateConnected"]=false,
                ["coordinateFrame"]=coordinates, ["payload"]=payload
            };
            var documentElements = new List<BsonDocument>();
            Extract(payload, "", "", doc, documentElements, areaStableId);
            // 한 원문 안에서도 같은 식별자의 복수 표현은 pointer로 구별한다. 임의 병합하지 않는다.
            var owners = documentElements.ToDictionary(x=>x["pointer"].AsString, x=>x["stableId"].AsString, StringComparer.Ordinal);
            var docKey = string.IsNullOrEmpty(stableId) ? source.SourcePath : stableId;
            ExtractRelations(payload, "", docKey, doc, owners, relations, areaStableId);
            doc["elementCount"] = documentElements.Count;
            doc["relationCount"] = relations.Count(x=>x["documentId"]==documentId);
            documents.Add(공간자료Json.Seal(doc)); elements.AddRange(documentElements.Select(공간자료Json.Seal));
        }
        if (elements.Count > 100_000 || relations.Count > 200_000) throw new InvalidDataException("SpatialExtractionBoundExceeded");
        // 경로 연결은 원문 Ref만 사용한다. 해석 대상과 연결 대상의 판본은 조회 묶음에서 별도로 해소한다.
        relations = relations.DistinctBy(x=>x["_id"].AsString).Select(공간자료Json.Seal).ToList();
        var manifest = new BsonArray(documents.Select(x=>new BsonDocument
        {
            ["documentId"]=x["_id"], ["sourcePath"]=x["sourcePath"], ["rawSha256"]=x["rawSha256"],
            ["areaStableId"]=x["areaStableId"], ["recordHash"]=x["recordHash"],
            ["elementCount"]=x["elementCount"], ["relationCount"]=x["relationCount"]
        }));
        var manifestHash = 공간자료Json.Hash(공간자료Json.Element(manifest, true).GetRawText() + "\n" + 공간자료CatalogCodes.AdapterVersion);
        var snapshot = new BsonDocument
        {
            ["_id"]="bundle:"+manifestHash, ["manifestHash"]=manifestHash, ["status"]="Staging",
            ["createdAtUtc"]=DateTimeOffset.UtcNow.ToString("O"), ["adapterVersion"]=공간자료CatalogCodes.AdapterVersion,
            ["documentCount"]=documents.Count, ["elementCount"]=elements.Count, ["relationCount"]=relations.Count,
            ["areaStableIds"]=new BsonArray(documents.Select(x=>x["areaStableId"].AsString).Where(x=>x.Length>0).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)),
            ["manifest"]=manifest, ["privateReviewOnly"]=true, ["gameStateConnected"]=false,
            ["authorityBoundary"]="원본 파일·MySQL 관측의 비공개 판본 조회. 승인·입주·영업·통행·게임 상태를 만들지 않는다."
        };
        return new(snapshot, documents, elements, relations);
    }

    private static string DocumentStableId(string kind, string schema, BsonDocument payload) => kind switch
    {
        "GraphMap" when schema is "simulation-world-graph-map.v1" or "ssalddel-focused-graph-map.v1" or "mirror-graph-map-plan.v3" => 공간자료Json.Text(payload, "graphStableId", "graphMapStableId"),
        "PlacementMap" => 공간자료Json.Text(payload, "profileStableId", "ruleStableId"),
        "SourceManifest" => 공간자료Json.Text(payload, "manifestStableId"),
        "ObjectCatalog" => 공간자료Json.Text(payload, "catalogStableId"),
        "ObjectCandidateProjection" => 공간자료Json.Text(payload, "projectionStableId"),
        "NeighborhoodPackage" => 공간자료Json.Text(payload, "packageStableId"),
        "NeighborhoodRegistry" => 공간자료Json.Text(payload, "registryStableId"),
        "ObservationInventory" => 공간자료Json.Text(payload, "inventoryStableId"),
        "SemanticLayerCatalog" => 공간자료Json.Text(payload, "catalogStableId"),
        "PresentationProjection" when schema == "neighborhood-unity-projection-handoff.v1"
            => 공간자료Json.Text(payload, "projectionStableId"),
        _ => ""
    };

    private static BsonValue CoordinateFrame(BsonDocument payload, string kind)
    {
        foreach (var name in new[] { "coordinateFrame", "coordinateSource", "coordinate" })
            if (payload.TryGetValue(name, out var frame) && frame.IsBsonDocument) return frame.DeepClone();
        if (kind != "Geometry") return BsonNull.Value;
        var result = new BsonDocument { ["method"]=payload["coordinateMethod"], ["units"]="meters", ["tileAssignment"]="BoundsCenterCandidateOnly" };
        foreach (var field in new[] { "originLatitude", "originLongitude", "offsetX", "offsetZ", "halfExtent" })
            result[field] = payload.GetValue(field, BsonNull.Value);
        return result;
    }

    private static void Extract(BsonValue value, string pointer, string field, BsonDocument doc,
        List<BsonDocument> output, string inheritedArea)
    {
        if (value.IsBsonDocument)
        {
            var ownArea=공간자료Json.Text(value.AsBsonDocument,"areaStableId");
            var area=ownArea.Length>0 ? ownArea : inheritedArea;
            foreach (var item in value.AsBsonDocument)
                Extract(item.Value, pointer+"/"+공간자료Json.Pointer(item.Name), item.Name, doc, output, area);
        }
        else if (value.IsBsonArray)
        {
            var array=value.AsBsonArray;
            for (var i=0; i<array.Count; i++)
            {
                var path=pointer+"/"+i;
                if (array[i].IsBsonDocument && ArrayKinds.TryGetValue(field, out var kind))
                {
                    var payload=array[i].AsBsonDocument;
                    var payloadArea=공간자료Json.Text(payload,"areaStableId");
                    var area=payloadArea.Length>0 ? payloadArea : inheritedArea;
                    var key=공간자료Json.Text(payload, IdentityKeys);
                    if (key.Length==0) key=doc["sourcePath"].AsString+"#"+path;
                    var record=new BsonDocument
                    {
                        ["_id"]="element:"+공간자료Json.Hash(doc["_id"].AsString+"\n"+path), ["documentId"]=doc["_id"],
                        ["stableId"]=key, ["pointer"]=path, ["kind"]=kind, ["dataset"]=doc["dataset"],
                        ["areaStableId"]=area,
                        ["reviewState"]="PendingHumanReview", ["layer"]=Layer(kind,payload),
                        ["semanticLayerStableId"]=SemanticLayer(kind,payload),
                        ["tile"]=Tile(kind,payload,doc), ["payload"]=payload.DeepClone()
                    };
                    output.Add(record);
                }
                Extract(array[i], path, field, doc, output, inheritedArea);
            }
        }
    }

    private static string Layer(string kind, BsonDocument payload) => kind switch
    {
        "Building" => "buildings", "Road" => "roads", "BuildingOverlay" => "building-use",
        "Address" => "addresses", "ActivityObservation" => "activities", "SampleLink" => "sample-links",
        "WorkflowModuleBinding" => "workflow-bindings", "CandidateGenerationProfile" => "candidate-generation",
        "ObjectArchetype" => "object-archetypes", "InteractionProfile" => "interaction-profiles",
        "RoleActionBinding" => "role-action-bindings", "ObjectCandidateBinding" => "object-candidates",
        "NeighborhoodPackageRegistration" => "neighborhood-packages", "ObservationCandidate" => "observations",
        "CompatibilityAlias" => "compatibility-aliases", "SemanticLayerDefinition" => "semantic-layers",
        "SemanticLayerBinding" => "layer-bindings", "CoordinateFrameRegistration" => "coordinate-frames",
        _ => 공간자료Json.Text(payload, "layerCode", "layerRef")
    };

    private static string SemanticLayer(string kind, BsonDocument payload)
    {
        var declared=공간자료Json.Text(payload,"semanticLayerStableId","semanticLayerRef");
        if(declared.Length>0) return declared;
        return kind switch
        {
            "Building" => "spatial-layer:building-footprint.v1",
            "Road" => "spatial-layer:road-centerline.v1",
            "Address" => "spatial-layer:address-link-private.v1",
            "ActivityObservation" or "ObservationCandidate" or "BuildingOverlay"
                => "spatial-layer:facility-observation.v1",
            "ScenarioOverlay" => "spatial-layer:scenario-overlay.v1",
            "CoordinateFrameRegistration" => "spatial-layer:coordinate-frame.v1",
            "ObjectCandidateBinding" when 공간자료Json.Text(payload,"objectArchetypeRef")
                .Contains("neutral-building",StringComparison.Ordinal)
                => "spatial-layer:building-footprint.v1",
            "ObjectCandidateBinding" when 공간자료Json.Text(payload,"objectArchetypeRef")
                .Contains("neutral-road-network",StringComparison.Ordinal)
                => "spatial-layer:road-centerline.v1",
            _ => ""
        };
    }

    private static string Tile(string kind, BsonDocument payload, BsonDocument doc)
    {
        var existing=공간자료Json.Text(payload, "tileRef");
        if(existing.Length>0) return existing;
        var id=공간자료Json.Text(payload,"id"); if(id.StartsWith("tile:",StringComparison.Ordinal)) return id;
        if(doc["kind"]!="Geometry" || doc["coordinateFrame"].IsBsonNull) return "";
        var frame=doc["coordinateFrame"].AsBsonDocument;
        if(공간자료Json.Text(frame,"method")!="WGS84-ECEF-ENU-at-zero-altitude") return "";
        double x,z;
        if(kind=="Building" && payload.TryGetValue("points",out var points) && points.IsBsonArray && points.AsBsonArray.Count>=4)
        {
            var xs=points.AsBsonArray.Select(p=>p["x"].ToDouble()).ToArray();
            var zs=points.AsBsonArray.Select(p=>p["z"].ToDouble()).ToArray();
            x=(xs.Min()+xs.Max())/2; z=(zs.Min()+zs.Max())/2;
        }
        else if(kind=="Road") { x=(payload["x1"].ToDouble()+payload["x2"].ToDouble())/2; z=(payload["z1"].ToDouble()+payload["z2"].ToDouble())/2; }
        else return "";
        var tileSize=frame.GetValue("tileSizeMeters",500).ToDouble();
        var tx=(int)Math.Floor((x-frame["offsetX"].ToDouble())/tileSize); var tz=(int)Math.Floor((z-frame["offsetZ"].ToDouble())/tileSize);
        var scope=공간자료Json.Text(frame,"tileScopeCode");
        if(scope.Length==0) scope=doc["areaStableId"].AsString switch
        {
            "region:kr:bjd:1126010100" => "myeonmok",
            { Length:>0 } area => area.Split(':')[^1],
            _ => "unscoped"
        };
        return $"tile:{scope}:{tileSize:0}m:x{tx}:z{tz}";
    }

    private static void ExtractRelations(BsonValue value, string pointer, string owner, BsonDocument doc,
        IReadOnlyDictionary<string,string> owners, List<BsonDocument> output, string inheritedArea)
    {
        if(owners.TryGetValue(pointer,out var found)) owner=found;
        if(value.IsBsonDocument)
        {
            var obj=value.AsBsonDocument;
            var ownArea=공간자료Json.Text(obj,"areaStableId");
            var area=ownArea.Length>0 ? ownArea : inheritedArea;
            var from=공간자료Json.Text(obj,"fromNodeRef","fromNodeId");
            var to=공간자료Json.Text(obj,"toNodeRef","toNodeId");
            if(from.Length>0 && to.Length>0) AddRelation(doc,pointer,from,to,"GraphEdge:"+공간자료Json.Text(obj,"relationCode","actionCode","edgeKindCode"),obj,output,area);
            foreach(var item in obj)
            {
                var path=pointer+"/"+공간자료Json.Pointer(item.Name);
                if(IsReference(item.Name))
                {
                    if(item.Value.IsString && item.Value.AsString.Length>0) AddRelation(doc,path,owner,item.Value.AsString,"ExplicitField:"+item.Name,obj,output,area);
                    else if(item.Value.IsBsonArray)
                    {
                        for(var i=0;i<item.Value.AsBsonArray.Count;i++)
                            if(item.Value[i].IsString && item.Value[i].AsString.Length>0) AddRelation(doc,path+"/"+i,owner,item.Value[i].AsString,"ExplicitField:"+item.Name,obj,output,area);
                    }
                    else if(item.Value.IsBsonDocument && 공간자료Json.Text(item.Value.AsBsonDocument,"path") is { Length:>0 } target)
                        AddRelation(doc,path+"/path",owner,target,"ExplicitField:"+item.Name,item.Value.AsBsonDocument,output,area);
                }
                ExtractRelations(item.Value,path,owner,doc,owners,output,area);
            }
        }
        else if(value.IsBsonArray)
            for(var i=0;i<value.AsBsonArray.Count;i++) ExtractRelations(value[i],pointer+"/"+i,owner,doc,owners,output,inheritedArea);
    }

    private static bool IsReference(string field) => field.EndsWith("Ref",StringComparison.Ordinal) ||
        field.EndsWith("Refs",StringComparison.Ordinal) || field is "worldInteractionId" or "worldInteractionIds" or
        "observationIds" or "addressStableId" or "repoRelativePath" or "unityProjectRelativePath" or "instanceProfiles";

    private static void AddRelation(BsonDocument doc,string pointer,string from,string to,string kind,BsonDocument evidence,
        List<BsonDocument> output,string areaStableId)
    {
        // 원문 자기 식별자 필드를 관계로 중복 해석하지 않는다.
        if(from==to && !kind.StartsWith("GraphEdge:",StringComparison.Ordinal)) return;
        output.Add(new BsonDocument
        {
            ["_id"]="relation:"+공간자료Json.Hash(doc["_id"].AsString+"\n"+pointer+"\n"+kind+"\n"+from+"\n"+to),
            ["documentId"]=doc["_id"], ["pointer"]=pointer, ["fromKey"]=from, ["toKey"]=to,
            ["kind"]=kind, ["dataset"]=doc["dataset"], ["areaStableId"]=areaStableId,
            ["semanticLayerStableId"]=SemanticLayer("",evidence),
            ["evidenceCode"]="SourceExplicitReference",
            ["reviewState"]="PendingHumanReview", ["expectedRevision"]=공간자료Json.Text(evidence,"expectedRevision"),
            ["expectedSha256"]=공간자료Json.Text(evidence,"expectedSha256","sourceHash"),
            ["sourceRelationState"]=공간자료Json.Text(evidence,"connectionStatus","status","stateCode","method"),
            ["authorityApproved"]=false
        });
    }
}
