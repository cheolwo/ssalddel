namespace Ssalddel.Contracts.Food;

public sealed class 음식점메뉴등록요청
{
    public Guid 클라이언트요청Id { get; set; }
    public string 메뉴명 { get; set; } = string.Empty;
    public string 설명 { get; set; } = string.Empty;
    public decimal 판매가 { get; set; }
    public string? 대표이미지Url { get; set; }
    public bool 공개여부 { get; set; }
    public bool 품절여부 { get; set; }
    public int 표시순서 { get; set; }
}

public sealed class 음식점메뉴수정요청
{
    public long 예상Revision { get; set; }
    public string 메뉴명 { get; set; } = string.Empty;
    public string 설명 { get; set; } = string.Empty;
    public decimal 판매가 { get; set; }
    public string? 대표이미지Url { get; set; }
    public bool 공개여부 { get; set; }
    public bool 품절여부 { get; set; }
    public int 표시순서 { get; set; }
}

public sealed class 음식점메뉴관리응답
{
    public long Id { get; set; }
    public long 음식점Id { get; set; }
    public string 메뉴명 { get; set; } = string.Empty;
    public string 설명 { get; set; } = string.Empty;
    public decimal 판매가 { get; set; }
    public string? 대표이미지Url { get; set; }
    public bool 공개여부 { get; set; }
    public bool 품절여부 { get; set; }
    public int 표시순서 { get; set; }
    public long Revision { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public bool 새로생성됨 { get; set; }
}
