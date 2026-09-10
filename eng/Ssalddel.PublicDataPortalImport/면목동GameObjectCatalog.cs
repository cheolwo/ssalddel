using System.Text;
using System.Text.Json;
using MongoDB.Bson;
using Ssalddel.Services.WorldProjection.SpatialCatalog;
using Ssalddel.WorkflowRules;
using Ssalddel.WorkflowRules.Contracts;

internal static class 면목동GameObjectCatalog
{
    internal const string CatalogRelative = "eng/world-seedbeds/object-catalogs/jungnang-myeonmok-game-objects.v1.json";
    internal const string ProjectionSourcePath = "generated:myeonmok-game-object-candidates.r2.json";
    internal const string HandoffFileName = "myeonmok-game-object-unity-handoff.r2.json";

    internal static 공간자료Source BuildProjection(string root, IReadOnlyList<공간자료Source> physicalSources)
    {
        var catalogSource = physicalSources.Single(source => source.Kind == "ObjectCatalog");
        var geometrySource = physicalSources.Single(source => source.Kind == "Geometry");
        var catalog = 공간자료Json.Parse(catalogSource.Bytes);
        var geometry = 공간자료Json.Parse(geometrySource.Bytes);

        ValidateCatalog(root, catalog);
        if (공간자료Json.Text(geometry, "coordinateMethod") != "WGS84-ECEF-ENU-at-zero-altitude")
            throw new InvalidDataException("MyeonmokObjectCatalogCoordinateFrameUnsupported");

        var buildings = geometry["buildings"].AsBsonArray.Select(value => value.AsBsonDocument)
            .OrderBy(value => 공간자료Json.Text(value, "id"), StringComparer.Ordinal).ToArray();
        var roads = geometry["roads"].AsBsonArray.Select(value => value.AsBsonDocument)
            .OrderBy(value => 공간자료Json.Text(value, "id"), StringComparer.Ordinal).ToArray();
        if (buildings.Length != 602 || roads.Length != 2397)
            throw new InvalidDataException("MyeonmokObjectCatalogGeometryCountChanged");

        var candidates = new BsonArray();
        foreach (var building in buildings)
        {
            var sourceId = RequiredText(building, "id");
            candidates.Add(new BsonDocument
            {
                ["candidateStableId"] = "game-object-candidate:myeonmok:building:" + sourceId,
                ["objectArchetypeRef"] = "game-object-archetype:myeonmok-neutral-building.v1",
                ["sourceElementRef"] = sourceId,
                ["tileRef"] = BuildingTile(geometry, building),
                ["classificationCode"] = "NEUTRAL",
                ["candidateState"] = "Candidate",
                ["placementEligibility"] = "BackdropOnly",
                ["heightEvidenceCode"] = RequiredText(building, "heightKind"),
                ["reviewState"] = "PendingHumanReview",
                ["sceneReady"] = false,
                ["isExecutionAuthority"] = false,
            });
        }

        var roadIds = new BsonArray(roads.Select(road => (BsonValue)RequiredText(road, "id")));
        var roadTiles = new BsonArray(roads.Select(road => RoadTile(geometry, road))
            .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal));
        candidates.Add(new BsonDocument
        {
            ["candidateStableId"] = "game-object-candidate:myeonmok:road-network.v1",
            ["objectArchetypeRef"] = "game-object-archetype:myeonmok-neutral-road-network.v1",
            ["sourceElementRefs"] = roadIds,
            ["tileRefs"] = roadTiles,
            ["sourceElementCount"] = roads.Length,
            ["classificationCode"] = "NEUTRAL",
            ["candidateState"] = "Candidate",
            ["placementEligibility"] = "BackdropOnly",
            ["traversalReady"] = false,
            ["reviewState"] = "PendingHumanReview",
            ["sceneReady"] = false,
            ["isExecutionAuthority"] = false,
        });

        var projection = new BsonDocument
        {
            ["schemaVersion"] = "myeonmok-game-object-candidate-projection.v1",
            ["projectionStableId"] = "game-object-candidate-projection:jungnang-myeonmok.r2",
            ["revision"] = "jungnang-myeonmok-game-object-candidates.r2",
            ["objectCatalogRef"] = RequiredText(catalog, "catalogStableId"),
            ["objectCatalogRevision"] = RequiredText(catalog, "revision"),
            ["objectCatalogSha256"] = 공간자료Json.Hash(catalogSource.Bytes),
            ["sourceGeometryRef"] = geometrySource.SourcePath,
            ["sourceGeometrySha256"] = 공간자료Json.Hash(geometrySource.Bytes),
            ["coordinateFrame"] = new BsonDocument
            {
                ["method"] = geometry["coordinateMethod"],
                ["originLatitude"] = geometry["originLatitude"],
                ["originLongitude"] = geometry["originLongitude"],
                ["offsetX"] = geometry["offsetX"],
                ["offsetZ"] = geometry["offsetZ"],
                ["tileSizeMeters"] = 500,
            },
            ["sourceElementCounts"] = new BsonDocument { ["buildings"] = buildings.Length, ["roads"] = roads.Length },
            ["candidateCounts"] = new BsonDocument { ["buildingBackdrop"] = buildings.Length, ["roadNetwork"] = 1 },
            ["privateDataIncluded"] = false,
            ["sceneReady"] = false,
            ["traversalReady"] = false,
            ["gameStateConnected"] = false,
            ["isExecutionAuthority"] = false,
            ["candidates"] = candidates,
        };
        return new 공간자료Source(ProjectionSourcePath, "jungnang-myeonmok", "ObjectCandidateProjection", Bytes(projection));
    }

    internal static byte[] BuildUnityHandoff(
        공간자료Source catalogSource,
        공간자료Source projectionSource,
        공간자료ImportBatch batch)
    {
        var catalog = 공간자료Json.Parse(catalogSource.Bytes);
        var projection = 공간자료Json.Parse(projectionSource.Bytes);
        var candidateSummaries = new BsonArray(projection["candidates"].AsBsonArray.Select(value =>
        {
            var item = value.AsBsonDocument;
            var result = new BsonDocument
            {
                ["candidateStableId"] = item["candidateStableId"],
                ["objectArchetypeRef"] = item["objectArchetypeRef"],
                ["classificationCode"] = item["classificationCode"],
                ["candidateState"] = item["candidateState"],
                ["placementEligibility"] = item["placementEligibility"],
                ["sceneReady"] = false,
            };
            if (item.TryGetValue("sourceElementRef", out var sourceRef)) result["sourceElementRef"] = sourceRef;
            if (item.TryGetValue("tileRef", out var tileRef)) result["tileRef"] = tileRef;
            if (item.TryGetValue("heightEvidenceCode", out var height)) result["heightEvidenceCode"] = height;
            if (item.TryGetValue("sourceElementCount", out var count)) result["sourceElementCount"] = count;
            if (item.TryGetValue("tileRefs", out var tiles)) result["tileRefs"] = tiles;
            return (BsonValue)result;
        }));

        var handoff = new BsonDocument
        {
            ["schemaVersion"] = "myeonmok-game-object-unity-handoff.v1",
            ["handoffStableId"] = "unity-handoff:jungnang-myeonmok-game-objects.r2",
            ["revision"] = catalog["revision"],
            ["bundleId"] = batch.Snapshot["_id"],
            ["manifestHash"] = batch.Snapshot["manifestHash"],
            ["catalogStableId"] = catalog["catalogStableId"],
            ["catalogSha256"] = 공간자료Json.Hash(catalogSource.Bytes),
            ["candidateProjectionRef"] = projection["projectionStableId"],
            ["candidateProjectionSha256"] = 공간자료Json.Hash(projectionSource.Bytes),
            ["classificationRevision"] = catalog["classificationRevision"],
            ["engineCode"] = catalog["engineCode"],
            ["engineMeaningRevision"] = catalog["engineMeaningRevision"],
            ["graphMapRef"] = catalog["graphMapRef"],
            ["placementMapRef"] = catalog["placementMapRef"],
            ["authorityBoundary"] = catalog["authorityBoundary"].DeepClone(),
            ["workflowBindings"] = catalog["workflowBindings"].DeepClone(),
            ["archetypes"] = catalog["archetypes"].DeepClone(),
            ["interactionProfiles"] = catalog["interactionProfiles"].DeepClone(),
            ["roleActionBindings"] = catalog["roleActionBindings"].DeepClone(),
            ["candidateCounts"] = projection["candidateCounts"].DeepClone(),
            ["candidates"] = candidateSummaries,
            ["unityExecutionState"] = "Deferred",
            ["sceneReady"] = false,
            ["gameStateConnected"] = false,
            ["isExecutionAuthority"] = false,
        };
        return Bytes(handoff);
    }

    private static void ValidateCatalog(string root, BsonDocument catalog)
    {
        if (RequiredText(catalog, "schemaVersion") != "myeonmok-game-object-catalog.v1"
            || RequiredText(catalog, "catalogStableId") != "game-object-catalog:jungnang-myeonmok.v1")
            throw new InvalidDataException("MyeonmokObjectCatalogIdentityMismatch");

        VerifyHash(root, catalog, "graphMapPath", "graphMapSha256");
        VerifyHash(root, catalog, "placementMapPath", "placementMapSha256");
        VerifyHash(root, catalog, "sourceManifestPath", "sourceManifestSha256");
        VerifyHash(root, catalog, "classificationProjectionRef", "classificationProjectionSha256");
        VerifySourceIdentity(root, catalog, "graphMapPath", "graphMapRef", "graphMapRevision", "graphStableId");
        VerifySourceIdentity(root, catalog, "placementMapPath", "placementMapRef", "placementMapRevision", "profileStableId");
        VerifySourceIdentity(root, catalog, "sourceManifestPath", "sourceManifestRef", "sourceManifestRevision", "manifestStableId");

        var classificationPath = Path.Combine(root, RequiredText(catalog, "classificationProjectionRef"));
        var classification = 공간자료Json.Parse(File.ReadAllBytes(classificationPath));
        var classificationRevision = RequiredText(classification, "sourceRevision");
        if (classificationRevision != RequiredText(catalog, "classificationRevision"))
            throw new InvalidDataException("MyeonmokObjectCatalogClassificationRevisionMismatch");

        var engine = BusinessWorkflowRuleEngine.기본;
        var engineInfo = engine.Engine정보조회();
        if (engineInfo.EngineCode != "BusinessWorkflow"
            || engineInfo.MeaningRevision != "business-workflow-engine.r1"
            || engineInfo.ClassificationMetadata?.SchemeCode
                != WorkflowClassificationSchemeCodes.FiveElementGwae
            || engineInfo.ClassificationMetadata.MeaningRevision
                != RequiredText(catalog, "engineMeaningRevision")
            || engineInfo.IsExecutionAuthority)
            throw new InvalidDataException("MyeonmokObjectCatalogEngineMismatch");
        var modules = engine.전체Module조회().ToDictionary(module => module.ModuleCode, StringComparer.Ordinal);
        var workflowBindings = Array(catalog, "workflowBindings");
        if (workflowBindings.Length != modules.Count) throw new InvalidDataException("MyeonmokObjectCatalogModuleCountMismatch");
        foreach (var binding in workflowBindings)
        {
            var moduleCode = RequiredText(binding, "moduleCode");
            if (!modules.TryGetValue(moduleCode, out var module)
                || module.ClassificationMetadata?.PrimaryCode
                    != RequiredText(binding, "primaryRoleGwaeCode")
                || module.MeaningRevision != RequiredText(binding, "meaningRevision")
                || module.IsExecutionAuthority)
                throw new InvalidDataException("MyeonmokObjectCatalogModuleBindingMismatch:" + moduleCode);
        }

        var archetypes = Unique(Array(catalog, "archetypes"), "objectArchetypeStableId");
        foreach (var archetype in archetypes.Values)
        {
            if (archetype.GetValue("isExecutionAuthority", BsonBoolean.True).ToBoolean())
                throw new InvalidDataException("MyeonmokObjectCatalogAuthorityLeak");
            var gwae = RequiredText(archetype, "primaryRoleGwaeCode");
            var element = RequiredText(archetype, "fiveElementCode");
            if (gwae == "NONE")
            {
                if (element != "NEUTRAL" || RequiredText(archetype, "placementEligibility") != "BackdropOnly")
                    throw new InvalidDataException("MyeonmokObjectCatalogNeutralBoundaryMismatch");
            }
            else if (!ExpectedElements.TryGetValue(gwae, out var expected) || expected != element)
                throw new InvalidDataException("MyeonmokObjectCatalogElementMismatch:" + archetype["objectArchetypeStableId"]);
        }

        var classificationItems = Unique(Array(classification, "items"), "wiId");
        var profiles = Unique(Array(catalog, "interactionProfiles"), "profileStableId");
        var profilesByWi = new Dictionary<string, BsonDocument>(StringComparer.Ordinal);
        foreach (var profile in profiles.Values)
        {
            var wiId = RequiredText(profile, "wiId");
            if (!profilesByWi.TryAdd(wiId, profile)) throw new InvalidDataException("MyeonmokObjectCatalogDuplicateWi:" + wiId);
            if (!classificationItems.TryGetValue(wiId, out var classified))
                throw new InvalidDataException("MyeonmokObjectCatalogWiMissing:" + wiId);
            CompareGwae(profile, classified, "actionGwaeCode", "actionGwae");
            CompareGwae(profile, classified, "operationGwaeCode", "operationGwae");
            CompareGwae(profile, classified, "targetGwaeCode", "targetGwae");
            CompareGwae(profile, classified, "supportGwaeCode", "supportGwae");
            if (RequiredText(profile, "classificationRevision") != classificationRevision
                || profile.GetValue("isExecutionAuthority", BsonBoolean.True).ToBoolean())
                throw new InvalidDataException("MyeonmokObjectCatalogProfileBoundaryMismatch:" + wiId);
            if (RequiredText(profile, "bindingReadinessCode") == "EngineReady")
            {
                var decision = engine.판정(new BusinessWorkflowTransitionRequest
                {
                    요청ModuleCode = RequiredText(profile, "requestedModuleCode"),
                    업무흐름코드 = RequiredText(profile, "workflowCode"),
                    현재상태코드 = RequiredText(profile, "currentStateCode"),
                    목표상태코드 = RequiredText(profile, "targetStateCode"),
                });
                if (!decision.허용여부) throw new InvalidDataException("MyeonmokObjectCatalogEngineBindingBlocked:" + wiId);
            }
        }

        var roleBindings = Unique(Array(catalog, "roleActionBindings"), "bindingStableId");
        foreach (var binding in roleBindings.Values)
        {
            var profileId = RequiredText(binding, "profileStableId");
            var objectId = RequiredText(binding, "objectArchetypeRef");
            if (!profiles.TryGetValue(profileId, out var profile) || !archetypes.TryGetValue(objectId, out var archetype))
                throw new InvalidDataException("MyeonmokObjectCatalogRoleActionRefMissing");
            var expectedGwae = RequiredText(binding, "expectedGwaeCode");
            var profileField = RequiredText(binding, "participationKindCode") switch
            {
                "Action" => "actionGwaeCode", "Target" => "targetGwaeCode",
                "Operation" => "operationGwaeCode", "Support" => "supportGwaeCode",
                _ => throw new InvalidDataException("MyeonmokObjectCatalogParticipationKindUnsupported")
            };
            if (expectedGwae.Length > 0 && expectedGwae != OptionalText(profile, profileField))
                throw new InvalidDataException("MyeonmokObjectCatalogRoleActionGwaeMismatch");
            if (!archetype["workflowModuleCodes"].AsBsonArray.Any(value =>
                    value.IsString && value.AsString == RequiredText(profile, "requestedModuleCode")))
                throw new InvalidDataException("MyeonmokObjectCatalogObjectModuleMismatch:" + objectId);
        }
        foreach (var profile in profiles.Values)
        {
            var profileId = RequiredText(profile, "profileStableId");
            if (!roleBindings.Values.Any(binding => RequiredText(binding, "profileStableId") == profileId
                    && RequiredText(binding, "participationKindCode") == "Action"
                    && binding.GetValue("required", BsonBoolean.False).ToBoolean()))
                throw new InvalidDataException("MyeonmokObjectCatalogRequiredSubjectMissing:" + profileId);
        }

        ValidateResolverAssembly(catalog, archetypes.Values, profiles.Values, roleBindings.Values);
    }

    private static void ValidateResolverAssembly(
        BsonDocument catalog,
        IEnumerable<BsonDocument> archetypes,
        IEnumerable<BsonDocument> profiles,
        IEnumerable<BsonDocument> roleBindings)
    {
        var source = "object-catalog:" + RequiredText(catalog, "revision");
        var definitions = archetypes.Select(value => new BusinessObjectDefinition
        {
            ObjectArchetypeStableId = RequiredText(value, "objectArchetypeStableId"),
            DisplayName = RequiredText(value, "displayName"),
            MeaningRevision = RequiredText(catalog, "revision"),
            WorkflowModuleCodes = value["workflowModuleCodes"].AsBsonArray.Select(item => item.AsString).ToArray(),
            SourceStableIds = [source],
            ClassificationMetadata = Classification(
                "ObjectRole",
                RequiredText(value, "primaryRoleGwaeCode"),
                RequiredText(value, "fiveElementCode"),
                RequiredText(value, "baseColorToken"),
                RequiredText(catalog, "engineMeaningRevision"),
                source),
            IsExecutionAuthority = false,
        }).ToArray();
        var profileContracts = profiles.Select(value => new BusinessInteractionProfile
        {
            ProfileStableId = RequiredText(value, "profileStableId"),
            WiId = RequiredText(value, "wiId"),
            WorkflowCode = RequiredText(value, "workflowCode"),
            RequestedModuleCode = RequiredText(value, "requestedModuleCode"),
            ClassificationRevision = RequiredText(value, "classificationRevision"),
            SourceStableIds = [source],
            ClassificationMetadata = new[]
            {
                Classification("Action", OptionalText(value, "actionGwaeCode"), "", "", RequiredText(value, "classificationRevision"), source),
                Classification("Operation", OptionalText(value, "operationGwaeCode"), "", "", RequiredText(value, "classificationRevision"), source),
                Classification("Target", OptionalText(value, "targetGwaeCode"), "", "", RequiredText(value, "classificationRevision"), source),
                Classification("Support", OptionalText(value, "supportGwaeCode"), "", "", RequiredText(value, "classificationRevision"), source),
            }.Where(item => item.PrimaryCode.Length > 0).ToArray(),
            IsExecutionAuthority = false,
        }).ToArray();
        var bindingContracts = roleBindings.Select(value => new BusinessObjectRoleBinding
        {
            BindingStableId = RequiredText(value, "bindingStableId"),
            ProfileStableId = RequiredText(value, "profileStableId"),
            ObjectArchetypeStableId = RequiredText(value, "objectArchetypeRef"),
            ParticipationKindCode = RequiredText(value, "participationKindCode"),
            AuthorityCode = OptionalText(value, "authorityCode"),
            ClassificationMetadata = Classification(
                "BindingExpected",
                OptionalText(value, "expectedGwaeCode"),
                "",
                "",
                RequiredText(catalog, "engineMeaningRevision"),
                source),
            Required = value.GetValue("required", BsonBoolean.False).ToBoolean(),
        }).ToArray();
        var resolver = new BusinessObjectInteractionResolver(definitions, profileContracts, bindingContracts);
        var profileSource = profiles.ToDictionary(value => RequiredText(value, "profileStableId"), StringComparer.Ordinal);

        foreach (var profile in profileContracts.Where(value =>
                     RequiredText(profileSource[value.ProfileStableId], "bindingReadinessCode") == "EngineReady"))
        {
            var bindings = bindingContracts.Where(value => value.ProfileStableId == profile.ProfileStableId).ToArray();
            var subjectBinding = bindings.Single(value => value.ParticipationKindCode == BusinessObjectParticipationKindCodes.행위 && value.Required);
            var targetBinding = bindings.SingleOrDefault(value => value.ParticipationKindCode == BusinessObjectParticipationKindCodes.대상 && value.Required);
            var tools = bindings.Where(value => value.Required && value.ParticipationKindCode is
                    BusinessObjectParticipationKindCodes.작용 or BusinessObjectParticipationKindCodes.보조)
                .Select(ObjectRef).ToArray();
            var sourceProfile = profileSource[profile.ProfileStableId];
            var decision = resolver.판정(new BusinessObjectInteractionRequest
            {
                WiId = profile.WiId,
                WorkflowCode = profile.WorkflowCode,
                CurrentStateCode = RequiredText(sourceProfile, "currentStateCode"),
                TargetStateCode = RequiredText(sourceProfile, "targetStateCode"),
                Subject = ObjectRef(subjectBinding),
                Target = targetBinding == null ? null : ObjectRef(targetBinding),
                ToolsOrFacilities = tools,
            });
            if (!decision.허용여부 || decision.IsExecutionAuthority)
                throw new InvalidDataException("MyeonmokObjectCatalogResolverBindingBlocked:" + profile.WiId);
        }

        static BusinessObjectReference ObjectRef(BusinessObjectRoleBinding binding) => new()
        {
            InstanceStableId = "catalog-validation:" + binding.BindingStableId,
            ObjectArchetypeStableId = binding.ObjectArchetypeStableId,
            AuthorityCodes = string.IsNullOrWhiteSpace(binding.AuthorityCode) ? [] : [binding.AuthorityCode],
        };
    }

    private static WorkflowClassificationMetadata Classification(
        string scopeCode,
        string primaryCode,
        string elementCode,
        string displayToken,
        string meaningRevision,
        string sourceStableId)
        => new()
        {
            SchemeCode = WorkflowClassificationSchemeCodes.FiveElementGwae,
            ScopeCode = scopeCode,
            PrimaryCode = primaryCode,
            ElementCode = elementCode,
            DisplayToken = displayToken,
            MeaningRevision = meaningRevision,
            SourceStableIds = [sourceStableId],
            IsExecutionAuthority = false,
        };

    private static readonly IReadOnlyDictionary<string, string> ExpectedElements = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["JIN"] = "WOOD", ["RI"] = "FIRE", ["GAN"] = "EARTH", ["GAM"] = "WATER", ["TAE"] = "METAL",
    };

    private static void VerifyHash(string root, BsonDocument catalog, string pathField, string hashField)
    {
        var path = Path.GetFullPath(Path.Combine(root, RequiredText(catalog, pathField)));
        if (!path.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || !File.Exists(path) || 공간자료Json.Hash(File.ReadAllBytes(path)) != RequiredText(catalog, hashField))
            throw new InvalidDataException("MyeonmokObjectCatalogSourceHashMismatch:" + pathField);
    }

    private static void VerifySourceIdentity(
        string root,
        BsonDocument catalog,
        string pathField,
        string referenceField,
        string revisionField,
        string sourceIdentityField)
    {
        var payload = 공간자료Json.Parse(File.ReadAllBytes(Path.Combine(root, RequiredText(catalog, pathField))));
        if (RequiredText(payload, sourceIdentityField) != RequiredText(catalog, referenceField)
            || RequiredText(payload, "revision") != RequiredText(catalog, revisionField))
            throw new InvalidDataException("MyeonmokObjectCatalogSourceIdentityMismatch:" + referenceField);
    }

    private static Dictionary<string, BsonDocument> Unique(BsonDocument[] values, string key)
    {
        var result = new Dictionary<string, BsonDocument>(StringComparer.Ordinal);
        foreach (var value in values)
            if (!result.TryAdd(RequiredText(value, key), value)) throw new InvalidDataException("MyeonmokObjectCatalogDuplicate:" + key);
        return result;
    }

    private static void CompareGwae(BsonDocument profile, BsonDocument classified, string profileField, string classifiedField)
    {
        if (OptionalText(profile, profileField) != OptionalText(classified, classifiedField))
            throw new InvalidDataException("MyeonmokObjectCatalogGwaeMismatch:" + RequiredText(profile, "wiId") + ":" + profileField);
    }

    private static BsonDocument[] Array(BsonDocument value, string field)
        => value[field].AsBsonArray.Select(item => item.AsBsonDocument).ToArray();

    private static string RequiredText(BsonDocument value, string field)
    {
        var result = OptionalText(value, field);
        return result.Length > 0 ? result : throw new InvalidDataException("MyeonmokObjectCatalogRequiredField:" + field);
    }

    private static string OptionalText(BsonDocument value, string field)
        => value.TryGetValue(field, out var result) && result.IsString ? result.AsString : string.Empty;

    private static string BuildingTile(BsonDocument frame, BsonDocument building)
    {
        var points = building["points"].AsBsonArray.Select(value => value.AsBsonDocument).ToArray();
        if (points.Length < 4) throw new InvalidDataException("MyeonmokObjectCatalogBuildingFootprintMissing");
        var x = (points.Min(point => point["x"].ToDouble()) + points.Max(point => point["x"].ToDouble())) / 2d;
        var z = (points.Min(point => point["z"].ToDouble()) + points.Max(point => point["z"].ToDouble())) / 2d;
        return Tile(frame, x, z);
    }

    private static string RoadTile(BsonDocument frame, BsonDocument road)
        => Tile(frame, (road["x1"].ToDouble() + road["x2"].ToDouble()) / 2d,
            (road["z1"].ToDouble() + road["z2"].ToDouble()) / 2d);

    private static string Tile(BsonDocument frame, double x, double z)
    {
        var tx = (int)Math.Floor((x - frame["offsetX"].ToDouble()) / 500d);
        var tz = (int)Math.Floor((z - frame["offsetZ"].ToDouble()) / 500d);
        return $"tile:myeonmok:500m:x{tx}:z{tz}";
    }

    private static byte[] Bytes(BsonDocument value)
        => Encoding.UTF8.GetBytes(공간자료Json.Element(value, sensitive: true).GetRawText());
}
