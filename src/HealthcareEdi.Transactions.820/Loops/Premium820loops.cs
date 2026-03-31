using HealthcareEdi.Core.Attributes;
using HealthcareEdi.Core.Segments;
using HealthcareEdi.Transactions.Premium820.Segments;

namespace HealthcareEdi.Transactions.Premium820.Loops;

[EdiLoop("1000A")]
public class PremiumRemitterLoop
{
    public Nm1Segment Name { get; set; } = new();
    public N3Segment? Address { get; set; }
    public N4Segment? CityStateZip { get; set; }
    public string RemitterName => Name.FullName;
}

[EdiLoop("1000B")]
public class PremiumReceiverLoop
{
    public Nm1Segment Name { get; set; } = new();
    public N3Segment? Address { get; set; }
    public N4Segment? CityStateZip { get; set; }
    public string ReceiverName => Name.FullName;
}

[EdiLoop("2000")]
public class OrganizationSummaryLoop
{
    public EntSegment? Entity { get; set; }
    public List<RefSegment> References { get; set; } = [];
    public List<PremiumDetailLoop> PremiumDetails { get; set; } = [];
    public decimal TotalPremium => PremiumDetails.Sum(d => d.Remittance?.DetailPremiumAmount ?? 0);
}

[EdiLoop("2300")]
public class PremiumDetailLoop
{
    public RmrSegment? Remittance { get; set; }
    public List<RefSegment> References { get; set; } = [];
    public List<DtpSegment> Dates { get; set; } = [];

    public string? MemberPolicyNumber => References.FirstOrDefault(r => r.ReferenceIdQualifier == "1L")?.ReferenceId;
    public decimal PremiumAmount => Remittance?.DetailPremiumAmount ?? 0;
}
