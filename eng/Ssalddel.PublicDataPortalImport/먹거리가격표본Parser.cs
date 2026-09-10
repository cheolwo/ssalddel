using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Ssalddel.Domain.PublicData;
using 살뜰.Services.External.PublicData.Agriculture;

internal static class 먹거리가격표본Parser
{
    public const string Revision="potato-prices-20260908.r1";
    public const string Version="survey-date:2026-09-07;convert-kg:Y";
    public static List<외부데이터정규화Record> Parse(string json,string cls,DateTimeOffset collected)
    {
        Require(cls is "01" or "02","ClassInvalid");
        Require(Encoding.UTF8.GetByteCount(json)<=2*1024*1024,"PayloadTooLarge");
        using var document=JsonDocument.Parse(json);
        var root=document.RootElement;
        Require(root.ValueKind==JsonValueKind.Object && root.EnumerateObject().Count()==1 && root.TryGetProperty("data",out _),"SanitizedDataOnlyRequired");
        var data=root.GetProperty("data");
        Require(data.GetProperty("error_code").GetString()=="000","SourceNotSuccess");
        var items=data.GetProperty("item");
        Require(items.ValueKind==JsonValueKind.Array,"ItemsArrayRequired");
        var records=new List<외부데이터정규화Record>(); var ids=new HashSet<string>();
        foreach(var item in items.EnumerateArray())
        {
            Require(item.TryGetProperty("item_code",out var code) && code.ValueKind==JsonValueKind.String,"ItemCodeRequired");
            if(code.GetString()!="152") continue;
            var properties=item.EnumerateObject().ToArray();
            var fields=new[]{"item_name","item_code","kind_name","kind_code","rank","rank_code","unit"}
                .Concat(Enumerable.Range(1,7).SelectMany(i=>new[]{"day"+i,"dpr"+i})).ToArray();
            Require(properties.Length==fields.Length && properties.Select(x=>x.Name).Distinct().Count()==fields.Length
                && fields.All(x=>item.TryGetProperty(x,out var v)&&v.ValueKind==JsonValueKind.String),"ColumnsChanged");
            string Value(string key)=>item.GetProperty(key).GetString()!;
            Require(Value("item_name")=="감자" && Value("kind_code")=="01" && Value("kind_name")=="수미(노지)(1kg)","ProductMismatch");
            var rank=Value("rank_code");
            Require(rank is "04" or "05" && Value("rank")== (rank=="04"?"상품":"중품"),"GradeMismatch");
            Require(Value("day1")=="당일 (09/07)","SurveyDateMismatch");
            Require(Value("unit")== (cls=="01"?"100g":"20kg"),"SourceUnitChanged");
            Require(decimal.TryParse(Value("dpr1"),NumberStyles.AllowThousands|NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var price)&&price>0,"CurrentPriceInvalid");
            var dimension=$"class={cls};category=100;item=152;kind=01;rank={rank};region=ALL;convert-kg=Y";
            Require(ids.Add(dimension),"DuplicatePriceIdentity");
            var original=fields.Order(StringComparer.Ordinal).ToDictionary(x=>x,Value);
            var text=JsonSerializer.Serialize(original,new JsonSerializerOptions { Encoder=JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            Require(text.Length<=2000,"RecordTooLarge");
            var date=new DateTimeOffset(2026,9,7,0,0,0,TimeSpan.Zero);
            records.Add(new 외부데이터정규화Record {
                SourceId=FarmRealityDataSourceIds.Kamis,DatasetId=FarmRealityDataSourceIds.KamisPriceObservations,
                StableId=$"kamis:daily:2026-09-07:{cls}:100:152:01:{rank}:ALL:kg-request",
                RegionStableId="region:kr",MetricCode="food-price-source-observation",
                RecordKey=외부데이터RecordKey.Create(FarmRealityDataSourceIds.Kamis,FarmRealityDataSourceIds.KamisPriceObservations,"region:kr","food-price-source-observation",date,dimension),
                TextValue=text,NumericValue=null,UnitCode="source-record",EvidenceAsOfUtc=date,CollectedAtUtc=collected,
                SpatialPrecisionCode="source-all-regions",TemporalPrecisionCode="date-only",
                QualityCode="PendingHumanReview",LimitationCode="PrivateReviewOnly;NoPublication;NoRuntime;UnitReviewRequired;ConditionOmitted",
                DimensionKey=dimension,SourceVersion=Version,DataRevision=Revision,FirstSeenAtUtc=collected,LastSeenAtUtc=collected
            });
        }
        return records.OrderBy(x=>x.RecordKey,StringComparer.Ordinal).ToList();
    }
    public static int SelfTest(string json,DateTimeOffset collected)
    {
        var rows=Parse(json,"01",collected);
        Require(rows.Count==2 && rows.All(x=>x.NumericValue==null && x.QualityCode=="PendingHumanReview"),"ProjectionTest");
        Require(Parse(json,"01",collected).Select(x=>x.TextValue).SequenceEqual(rows.Select(x=>x.TextValue)),"DeterminismTest");
        using var doc=JsonDocument.Parse(json);
        var row=doc.RootElement.GetProperty("data").GetProperty("item").EnumerateArray().First(x=>x.GetProperty("item_code").GetString()=="152").GetRawText();
        var fixture="{\"data\":{\"error_code\":\"000\",\"item\":["+row+"]}}";
        Require(Parse(fixture.Replace("\"152\"","\"999\""),"01",collected).Count==0,"ScopeTest");
        var bad=new[]{fixture.Replace("000","900"),fixture.Replace("09/07","09/08"),fixture.Replace("100g","20kg"),
            fixture.Replace("3,188","NaN"),fixture.Replace("감자","다른품목"),
            fixture.Replace("\"04\"","\"99\""),"{\"condition\":{},\"data\":{}}",
            "{\"data\":{\"error_code\":\"000\",\"item\":["+row+","+row+"]}}"};
        foreach(var input in bad){var rejected=false;try{Parse(input,"01",collected);}catch(InvalidDataException){rejected=true;} Require(rejected,"NegativeTest");}
        return 3+bad.Length;
    }
    private static void Require(bool condition,string code){if(!condition)throw new InvalidDataException(code);}
}
