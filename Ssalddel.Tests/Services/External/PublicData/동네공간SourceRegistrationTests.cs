using Ssalddel.Contracts.Common.PublicData;
using 살뜰.Services.External.PublicData;
using 살뜰.Services.External.PublicData.Korea;

namespace Ssalddel.Tests.Services.External.PublicData;

public sealed class 동네공간SourceRegistrationTests
{
    [Fact]
    public void 기존출처대장과호환되고수집재배포를켜지않는다()
    {
        var registration = new 동네공간SourceRegistration();
        var catalog = new ExternalDataSourceCatalog(new PublicDataApiMetadataCatalog(), [registration]);
        Assert.Equal(11, registration.GetDefinitions().Count);
        Assert.All(registration.GetDefinitions(), item =>
        {
            Assert.Equal(item, catalog.GetRequired(item.SourceId, item.DatasetId));
            Assert.Equal(ExternalDataAccessMethod.ManualImport, item.AccessMethod);
            Assert.False(item.DefaultCollectionEnabled);
            Assert.False(item.ApiAvailable);
            Assert.False(item.RedistributionAllowed);
            Assert.False(item.RequiresCredential);
            Assert.Empty(item.CredentialReferences);
            Assert.NotEmpty(item.License);
            Assert.NotEmpty(item.UsageLimitations);
        });
    }

    [Theory]
    [InlineData("15012890")]
    [InlineData("15012896")]
    [InlineData("15012892")]
    public void 공공시설점자료는기존출처대장에수동검토범위로등록한다(string id)
    {
        var source=Assert.Single(new 동네공간SourceRegistration().GetDefinitions(), x=>x.DatasetId=="data-go-kr-"+id+"-standard");
        Assert.Equal("data-go-kr-local-government-spatial",source.SourceId);
        Assert.Equal("NeighborhoodGeography",source.DataDomain);
        Assert.Equal("https://www.data.go.kr/data/"+id+"/standard.do",source.OfficialSourceUrl);
        Assert.Contains("좌표 미확보",source.SpatialResolution);
        Assert.Contains("비공개 검토",source.UsageLimitations);
        Assert.False(source.DefaultCollectionEnabled);
        Assert.False(source.RedistributionAllowed);
    }

    [Fact]
    public void 기존건물원장의안정식별자를보존한다()
    {
        var definition = Assert.Single(new 동네공간SourceRegistration().GetDefinitions(),
            x => x.SourceId == VWorld건물통합정보ImportService.SourceId
                && x.DatasetId == VWorld건물통합정보ImportService.DatasetId);
        Assert.Equal(VWorld건물통합정보ImportService.DatasetId, definition.DatasetId);
        Assert.Contains("DBF 속성만", definition.UsageLimitations);
    }

    [Theory]
    [InlineData("seoul-jungnang-open-data","jungnang-apartment-status")]
    [InlineData("seoul-open-data","OA-16220")]
    [InlineData("seoul-open-data","OA-16165")]
    public void 면목동주소자료는원본과검토보류경계를유지한다(string source,string dataset)
    {
        var definition=Assert.Single(new 동네공간SourceRegistration().GetDefinitions(),x=>x.SourceId==source&&x.DatasetId==dataset);
        Assert.Contains("면목동",definition.SpatialResolution);
        Assert.Contains("검토보류",definition.UsageLimitations);
        Assert.False(definition.DefaultCollectionEnabled);
        Assert.False(definition.RedistributionAllowed);
        Assert.Contains("https://",definition.OfficialSourceUrl);
    }

    [Fact]
    public void 실제GIS와정의서는기존마스터와다른자료로등록한다()
    {
        var definitions = new 동네공간SourceRegistration().GetDefinitions();
        Assert.Equal(definitions.Count, definitions.Select(x => (x.SourceId, x.DatasetId)).Distinct().Count());
        var gis = Assert.Single(definitions, x => x.DatasetId == 동네공간SourceRegistration.Gis건물DatasetId);
        Assert.NotEqual(VWorld건물통합정보ImportService.DatasetId, gis.DatasetId);
        Assert.Contains("A0~A28", gis.DataFormat);
        Assert.Contains("불일치", gis.License);
        Assert.False(gis.RedistributionAllowed);
        var schema = Assert.Single(definitions, x => x.DatasetId == 동네공간SourceRegistration.Gis정의서DatasetId);
        Assert.Contains("XLSX", schema.DataFormat);
        Assert.NotEqual(gis.DatasetId, schema.DatasetId);
    }
}
