using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;

namespace Ssalddel.Simulation.Application
{
    public sealed class 로컬생활원천
    {
        public string Path { get; set; } = "";
        public string Sha256 { get; set; } = "";
    }
    public sealed class 로컬생활시작Payload
    {
        public string Schema { get; set; } = "offline-life.r1";
        public string ReviewState { get; set; } = "ApprovedSyntheticOnly";
        public 경영SimulationSession생성Request InitialRequest { get; set; } = new 경영SimulationSession생성Request();
        public JsonElement PlacementMap { get; set; }
        public 로컬생활원천[] Sources { get; set; } = Array.Empty<로컬생활원천>();
    }
    public sealed class 로컬생활시작묶음
    {
        public string ContentSha256 { get; set; } = "";
        public 로컬생활시작Payload Payload { get; set; } = new 로컬생활시작Payload();
    }

    /// <summary>준비 도구와 Unity가 함께 쓰는 파일 경계. HTTP/Mongo/Unity 참조와 자동 표본 대체가 없다.</summary>
    public static class 로컬생활시작자료
    {
        public const int MaxBundleBytes = 2 * 1024 * 1024;
        public const int MaxReportBytes = 12 * 1024 * 1024;
        public static string Hash(byte[] bytes) { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        public static T Copy<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
        public static byte[] JsonBytes<T>(T value) => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value));
        public static bool IsHash(string? value) => value != null && value.Length == 64 && value.All(Uri.IsHexDigit);
        public static void Require(bool ok, string code) { if (!ok) throw new InvalidDataException(code); }

        // 사본의 자기참조 필드만 비우고 계산한다. 원 입력은 변경하지 않는다.
        private static string PayloadHash(로컬생활시작Payload payload)
        {
            var copy = Copy(payload);
            Require(copy.InitialRequest?.LocalLife != null, "OfflineLifeBindingsMissing");
            copy.InitialRequest!.LocalLife!.SourceBundleSha256 = "";
            return CanonicalHash(JsonBytes(copy));
        }
        public static string CanonicalHash(byte[] bytes)
            => Hash(CanonicalBytes(bytes));
        public static byte[] CanonicalBytes(byte[] bytes)
        {
            using var doc = JsonDocument.Parse(bytes);
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream)) Canonical(doc.RootElement, writer);
            return stream.ToArray();
        }
        private static void Canonical(JsonElement e, Utf8JsonWriter w)
        {
            if (e.ValueKind == JsonValueKind.Object)
            {
                var props = e.EnumerateObject().ToArray();
                Require(props.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() == props.Length, "OfflineLifeDuplicateJsonProperty");
                w.WriteStartObject(); foreach (var p in props.OrderBy(x => x.Name, StringComparer.Ordinal)) { w.WritePropertyName(p.Name); Canonical(p.Value,w); } w.WriteEndObject();
            }
            else if (e.ValueKind == JsonValueKind.Array) { w.WriteStartArray(); foreach (var x in e.EnumerateArray()) Canonical(x,w); w.WriteEndArray(); }
            else if(e.ValueKind == JsonValueKind.String) w.WriteStringValue(e.GetString());
            else if(e.ValueKind == JsonValueKind.Number) w.WriteRawValue(e.TryGetDecimal(out var n)
                ? n.ToString("G29",System.Globalization.CultureInfo.InvariantCulture)
                : e.GetDouble().ToString("R",System.Globalization.CultureInfo.InvariantCulture));
            else e.WriteTo(w);
        }
        public static 로컬생활시작묶음 Seal(로컬생활시작Payload payload)
        {
            var result = new 로컬생활시작묶음 { Payload = Copy(payload), ContentSha256 = PayloadHash(payload) };
            result.Payload.InitialRequest.LocalLife!.SourceBundleSha256 = result.ContentSha256;
            Validate(result); return result;
        }
        public static 로컬생활시작묶음 Read(string path, string expectedContentHash)
        {
            Require(IsHash(expectedContentHash), "OfflineLifeExpectedHashRequired");
            var bytes = ReadBounded(path, MaxBundleBytes);
            CanonicalHash(bytes); // 중복 필드를 역직렬화 전에 거부한다.
            var bundle = JsonSerializer.Deserialize<로컬생활시작묶음>(bytes) ?? throw new InvalidDataException("OfflineLifeBundleMissing");
            Require(CanonicalHash(bytes) == CanonicalHash(JsonBytes(bundle)), "OfflineLifeUnmappedOrMissingJsonFields");
            Require(string.Equals(bundle.ContentSha256,expectedContentHash,StringComparison.Ordinal), "OfflineLifeUnexpectedBundle");
            Validate(bundle); return bundle;
        }
        public static void Validate(로컬생활시작묶음 bundle)
        {
            Require(bundle?.Payload?.InitialRequest?.LocalLife != null, "OfflineLifeBundleMissing");
            var p = bundle!.Payload;
            Require(p.Schema == "offline-life.r1" && p.ReviewState == "ApprovedSyntheticOnly", "OfflineLifeSchemaOrReviewInvalid");
            Require(IsHash(bundle.ContentSha256) && bundle.ContentSha256 == PayloadHash(p)
                && p.InitialRequest.LocalLife!.SourceBundleSha256 == bundle.ContentSha256, "OfflineLifePayloadHashMismatch");
            Require(p.Sources != null && p.Sources.Length >= 3 && p.Sources.Length <= 16 && p.Sources.All(x => x != null && IsHash(x.Sha256)
                && !string.IsNullOrWhiteSpace(x.Path) && !System.IO.Path.IsPathRooted(x.Path) && !x.Path.Contains(".."))
                && p.Sources.Select(x => x.Path).Distinct(StringComparer.Ordinal).Count() == p.Sources.Length, "OfflineLifeSourcesInvalid");
            var m = p.PlacementMap;
            Require(m.GetProperty("profileStableId").GetString() == "placement-map-profile:synthetic-neighborhood.v1"
                && m.GetProperty("revision").GetInt32() == 1, "OfflineLifeMapNotApproved");
            var shapes = m.GetProperty("instances").EnumerateArray().ToArray();
            Unique(shapes,9);
            foreach (var x in shapes) {
                var id = x.GetProperty("id").GetString()!; var b = 가상동네배치기준.시설(id);
                Require(x.GetProperty("x").GetDouble() == b.x && x.GetProperty("z").GetDouble() == b.z &&
                    x.GetProperty("width").GetDouble() == b.width && x.GetProperty("depth").GetDouble() == b.depth,
                    "OfflineLifeGeometryChanged");
                Require(x.GetProperty("kind").GetString() == (id.StartsWith("road:",StringComparison.Ordinal) ? "Road" : "Facility"), "OfflineLifeGeometryRoleChanged");
            }
            var anchors = m.GetProperty("anchors").EnumerateArray().ToArray(); Unique(anchors,44);
            foreach (var x in anchors) { var a = 가상동네배치기준.기준점(x.GetProperty("id").GetString()!);
                Require(x.GetProperty("x").GetDouble() == a.x && x.GetProperty("z").GetDouble() == a.z,"OfflineLifeAnchorChanged"); }
            var paths = m.GetProperty("paths").EnumerateArray().ToArray(); Unique(paths,14);
            foreach (var x in paths) {
                var expected = 가상동네배치기준.경로(x.GetProperty("id").GetString()!);
                var refs = x.GetProperty("anchorRefs").EnumerateArray().Select(a => a.GetString()!).ToArray();
                Require(refs.All(id => anchors.Any(a => a.GetProperty("id").GetString() == id)) &&
                    refs.Select(가상동네배치기준.기준점).SequenceEqual(expected) && x.GetProperty("bidirectional").GetBoolean(), "OfflineLifePathChanged");
            }
            // 현재 Core의 유효성 검사·권한 결속을 동일하게 사용한다. 실행 Tick/외부 효과는 없다.
            _ = new 경영SimulationSessionAggregate(Copy(p.InitialRequest));
            Require(JsonBytes(bundle).Length <= MaxBundleBytes,"OfflineLifeBundleTooLarge");
        }
        private static void Unique(JsonElement[] rows,int count) => Require(rows.Length == count &&
            rows.Select(x => x.GetProperty("id").GetString()).Distinct(StringComparer.Ordinal).Count() == count,"OfflineLifeMapCountOrDuplicate");
        public static byte[] ReadBounded(string path,int max)
        {
            SafePath(path); using var f = new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
            Require(f.Length > 0 && f.Length <= max,"OfflineLifeFileSizeInvalid");
            var bytes = new byte[(int)f.Length]; var n = 0;
            while(n < bytes.Length) { var read = f.Read(bytes,n,bytes.Length-n); if(read==0) throw new EndOfStreamException(); n += read; }
            return bytes;
        }
        public static void SafePath(string path)
        {
            for(var p = System.IO.Path.GetFullPath(path); !string.IsNullOrEmpty(p); p = System.IO.Path.GetDirectoryName(p))
                if(File.Exists(p) || Directory.Exists(p)) Require((File.GetAttributes(p)&FileAttributes.ReparsePoint)==0,"OfflineLifeReparsePathRejected");
        }
        public static void WriteOnce(string path,byte[] bytes,int max)
        {
            Require(bytes.Length > 0 && bytes.Length <= max,"OfflineLifeOutputTooLarge"); SafePath(path);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))!);
            if(File.Exists(path)) { Require(ReadBounded(path,max).SequenceEqual(bytes),"OfflineLifeOutputConflict"); return; }
            using var f = new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None); f.Write(bytes,0,bytes.Length); f.Flush(true);
        }
    }
}
