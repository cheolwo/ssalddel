using Ssalddel.Contracts.Common.PublicData;

namespace 살뜰.Services.External.PublicData.Korea;

/// <summary>도형 원본의 수동 확보 후보. 등록은 수집 실행·사용권 승인·게임 배치를 뜻하지 않는다.</summary>
public sealed class 동네공간SourceRegistration : IExternalDataSourceRegistration
{
    public const string Gis건물DatasetId = "gis-building-integrated-al-d010";
    public const string Gis정의서DatasetId = "national-spatial-column-definition";

    public IReadOnlyCollection<ExternalDataSourceDefinition> GetDefinitions() =>
    [
        Source("mois-juso", "road-name-address-electronic-map", "도로명주소 전자지도", "행정안전부",
            "https://www.data.go.kr/tcs/dss/selectFileDataDetailView.do?publicDataPk=15050413",
            "제공 원본 형식 확인 필요; 포털 PPTX는 안내 자료",
            "목록: 공공누리 제1유형; 실제 전달 파일 이용조건 별도 확인",
            "신청·본인 인증과 이용 목적 확인이 필요한 자료입니다. 건물·도로구간·실폭도로·출입구 후보이며 도로 방향·통행 권리는 별도 검토합니다."),
        Source(VWorld건물통합정보ImportService.SourceId, VWorld건물통합정보ImportService.DatasetId,
            "건물통합정보 마스터(기존 계약)", "국토교통부",
            "https://www.vworld.kr/", "UFID/PNU 필드가 있는 기존 DBF 계약",
            "기존 반입 원본의 이용조건 별도 확인",
            "기존 원장 ID를 보존합니다. ImportService는 DBF 속성만 정규화하며 AL_D010의 A0~A28 계약과 호환되지 않습니다. SHP 도형·출입구·도로 연결은 생성하지 않습니다."),
        Source(VWorld건물통합정보ImportService.SourceId, Gis건물DatasetId,
            "GIS건물통합정보(AL_D010)", "국토교통부",
            "https://www.vworld.kr/dtmk/dtmk_ntads_s002.do?svcCde=NA&dsId=18", "SHP/DBF A0~A28 묶음",
            "CC BY 표시와 CC BY-NC-ND 2.0 KR 상세 링크 불일치; 적용·재배포 검토보류",
            "서울 2026-08-09 원본을 확보했습니다. 기존 building-integrated-master와 별도 자료이며 필드·도형 정규화는 미연결입니다. 원본 계보 등록은 게임 활용 승인이 아닙니다."),
        Source(VWorld건물통합정보ImportService.SourceId, Gis정의서DatasetId,
            "국가중점 공간데이터 필드 정의서", "국토교통부",
            "https://www.vworld.kr/contents/국가중점데이터_컬럼정의서(26.01.02)_배포용.xlsx", "XLSX 정의서",
            "공급처 공개 정의서; 문서와 개별 데이터의 적용 권리는 별도 검토",
            "2026-01-02 판본의 전체데이터 AL_D010 필드 정의 근거입니다. 건물 도형·관측값·정규화 결과와 구분합니다."),
        Source("ngii", "digital-map-v2", "수치지도 V2.0", "국토지리정보원",
            "https://www.data.go.kr/data/15059719/fileData.do", "NGI 도형/속성",
            "목록: 공공누리 제1유형; 실제 전달 파일 이용조건 별도 확인",
            "공식 사이트 로그인·지역 도엽 다운로드 절차가 필요합니다. 목록 메타데이터를 도형 원본으로 취급하지 않습니다."),
        시설("15012890", "전국도시공원정보표준데이터", "조성 완료 공원. 원천 좌표·면적이며 공원 외곽/출입구가 아닙니다."),
        시설("15012896", "전국주차장정보표준데이터", "지자체 관리 공영/민영 주차장. 거주자우선주차 제외. 현재 이용가능·빈자리·통행은 별도입니다."),
        시설("15012892", "전국공중화장실표준데이터", "공중화장실 위치·개방시간의 원천 기준일 자료. 오래된 항목과 좌표 누락을 현행 시설로 추정하지 않습니다."),
        주소자료("seoul-jungnang-open-data","jungnang-apartment-status","중랑구 공동주택 현황",
            "https://www.data.go.kr/data/15006098/fileData.do","UTF-8 CSV","이용허락범위 제한 없음",
            "2026-02-24 / 30세대 이상 목록. 면목 행정동 명칭으로 선택하며 작은 빌라·다세대 전체, 공공임대 여부와 실제 입주를 인증하지 않습니다."),
        주소자료("seoul-open-data","OA-16220","중랑구 의원 인허가 정보",
            "https://data.jungnang.go.kr/openinf/sheetview.jsp?infId=OA-16220","JSON 공개 조회","공공누리 제1유형",
            "의원·치과의원·한의원 등. 영업/폐업 이력 구분, 원천 좌표 EPSG:5174는 미변환. 자료갱신일과 현재 영업 여부가 같지 않습니다."),
        주소자료("seoul-open-data","OA-16165","중랑구 병원 인허가 정보",
            "https://data.jungnang.go.kr/openinf/sheetview.jsp?infId=OA-16165","JSON 공개 조회","공공누리 제1유형",
            "병원·종합병원 등. 영업/폐업 이력 구분, 원천 좌표 EPSG:5174는 미변환. 진료 이용이나 실시간 운영 안내용이 아닙니다.")
    ];

    private static ExternalDataSourceDefinition 주소자료(string source,string dataset,string name,string url,string format,string license,string limitation)=>new()
    {
        SourceId=source,DatasetId=dataset,Name=name,Provider="서울특별시 중랑구",CountryCode="KOR",DataDomain="NeighborhoodGeography",
        OfficialSourceUrl=url,DocumentationUrl=url,AccessMethod=ExternalDataAccessMethod.ManualImport,CredentialType=ExternalDataCredentialType.None,
        RequiresCredential=false,DefaultCollectionEnabled=false,ApiAvailable=false,DataFormat=format,
        SpatialResolution="원천 주소로 면목동 선택; 실제 건물/출입구 결속 미확보",TemporalResolution="주택 파일 기준일 / 병의원 수집시각과 원천 갱신필드 구분",
        RefreshCadence="주택 연간 / 병의원 일별 원천",License=license,RedistributionAllowed=false,
        AttributionRequirement="중랑구·공식 URL·원문 hash·수집시각·기준일·선택 근거 표시",
        UsageLimitations=limitation+" 비공개 검토보류 축적이며 자동 게시·정기수집·Unity/게임상태 적용 없음.",LastVerifiedDate=new DateOnly(2026,9,8)
    };

    private static ExternalDataSourceDefinition 시설(string id, string name, string limitation) => new()
    {
        SourceId="data-go-kr-local-government-spatial", DatasetId="data-go-kr-"+id+"-standard", Name=name, Provider="지방자치단체 / 공공데이터포털 통합",
        CountryCode="KOR", DataDomain="NeighborhoodGeography", OfficialSourceUrl=$"https://www.data.go.kr/data/{id}/standard.do",
        DocumentationUrl=$"https://www.data.go.kr/biz/dcat/metadata/{id}.do", AccessMethod=ExternalDataAccessMethod.ManualImport,
        CredentialType=ExternalDataCredentialType.None, RequiresCredential=false, DefaultCollectionEnabled=false, ApiAvailable=false, DataFormat="JSON 공개 다운로드",
        SpatialResolution="주소 선택 + 원천 WGS84 점 또는 좌표 미확보; 행정경계 내 도형검사 미수행", TemporalResolution="행별 데이터기준일자",
        RefreshCadence="포털 월초 통합; 기관별 기준일 상이", License="2026-09-08 공식 DCAT: 이용허락범위 제한 없음",
        RedistributionAllowed=false, AttributionRequirement="기관·기준일·원문 hash·정규화·누락·공식 이용조건을 표시",
        UsageLimitations=limitation+" 일회성 중랑구 비공개 검토 반입. 정기수집·게시·권위상태/Unity 실행 자동연결 없음.", LastVerifiedDate=new DateOnly(2026,9,8)
    };

    private static ExternalDataSourceDefinition Source(string sourceId, string datasetId,
        string name, string provider, string url, string format, string license, string limitations) => new()
    {
        SourceId = sourceId, DatasetId = datasetId, Name = name, Provider = provider,
        CountryCode = "KOR", DataDomain = "NeighborhoodGeography",
        OfficialSourceUrl = url, DocumentationUrl = url,
        AccessMethod = ExternalDataAccessMethod.ManualImport,
        // 수동 파일 입력에는 API key를 요구하지 않는다. 공급처 다운로드 로그인은 위 이용 제한에 별도 표기한다.
        CredentialType = ExternalDataCredentialType.None, RequiresCredential = false,
        DefaultCollectionEnabled = false, ApiAvailable = false, DataFormat = format,
        SpatialResolution = "전달 원본의 도형·좌표계·축척 검토 필요",
        TemporalResolution = "전달 원본의 기준일과 판본",
        RefreshCadence = "수동 반입 시 원본별 확인",
        License = license, RedistributionAllowed = false,
        AttributionRequirement = "공급처·원본 기준일·판본·해시·전처리 근거와 이용조건 표시",
        UsageLimitations = limitations + " 자동 수집·재배포·실제 배차는 비활성입니다. 합성 업무 자료와 실제 지리 자료를 분리합니다.",
        LastVerifiedDate = new DateOnly(2026, 9, 6),
    };
}
