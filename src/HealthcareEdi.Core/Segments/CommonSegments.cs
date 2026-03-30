using HealthcareEdi.Core.Attributes;
using HealthcareEdi.Core.Parsing;

namespace HealthcareEdi.Core.Segments;

/// <summary>
/// Base class for all EDI segments providing raw element access.
/// </summary>
public abstract class EdiSegmentBase
{
    /// <summary>Raw elements as parsed from the segment string.</summary>
    public string[] RawElements { get; set; } = [];

    /// <summary>Gets element value at 1-based position, or empty string if not present.</summary>
    protected string Element(int position)
        => position < RawElements.Length ? RawElements[position] : string.Empty;
}

/// <summary>NM1 - Individual or Organizational Name</summary>
[EdiSegment("NM1")]
public class Nm1Segment : EdiSegmentBase
{
    public string EntityIdentifierCode { get; set; } = string.Empty;  // NM101 (85=Billing, 87=Pay-to, IL=Insured, QC=Patient, etc.)
    public string EntityTypeQualifier { get; set; } = string.Empty;   // NM102 (1=Person, 2=Non-Person)
    public string LastName { get; set; } = string.Empty;              // NM103
    public string FirstName { get; set; } = string.Empty;             // NM104
    public string MiddleName { get; set; } = string.Empty;            // NM105
    public string Prefix { get; set; } = string.Empty;                // NM106
    public string Suffix { get; set; } = string.Empty;                // NM107
    public string IdCodeQualifier { get; set; } = string.Empty;       // NM108 (XX=NPI, 46=ETIN, MI=MemberID)
    public string IdCode { get; set; } = string.Empty;                // NM109

    /// <summary>Convenience: Full name formatted as "Last, First Middle".</summary>
    public string FullName => EntityTypeQualifier == "2"
        ? LastName // Org name is in LastName for non-person entities
        : $"{LastName}, {FirstName}{(string.IsNullOrEmpty(MiddleName) ? "" : $" {MiddleName}")}".TrimEnd(' ', ',');

    public static Nm1Segment Parse(string[] elements)
    {
        return new Nm1Segment
        {
            RawElements = elements,
            EntityIdentifierCode = elements.ElementAtOrDefault(1) ?? "",
            EntityTypeQualifier = elements.ElementAtOrDefault(2) ?? "",
            LastName = elements.ElementAtOrDefault(3) ?? "",
            FirstName = elements.ElementAtOrDefault(4) ?? "",
            MiddleName = elements.ElementAtOrDefault(5) ?? "",
            Prefix = elements.ElementAtOrDefault(6) ?? "",
            Suffix = elements.ElementAtOrDefault(7) ?? "",
            IdCodeQualifier = elements.ElementAtOrDefault(8) ?? "",
            IdCode = elements.ElementAtOrDefault(9) ?? "",
        };
    }
}

/// <summary>REF - Reference Identification (repeating by qualifier)</summary>
[EdiSegment("REF")]
public class RefSegment : EdiSegmentBase
{
    public string ReferenceIdQualifier { get; set; } = string.Empty;  // REF01
    public string ReferenceId { get; set; } = string.Empty;           // REF02
    public string Description { get; set; } = string.Empty;           // REF03

    public static RefSegment Parse(string[] elements)
    {
        return new RefSegment
        {
            RawElements = elements,
            ReferenceIdQualifier = elements.ElementAtOrDefault(1) ?? "",
            ReferenceId = elements.ElementAtOrDefault(2) ?? "",
            Description = elements.ElementAtOrDefault(3) ?? "",
        };
    }
}

/// <summary>N3 - Address Information</summary>
[EdiSegment("N3")]
public class N3Segment : EdiSegmentBase
{
    public string AddressLine1 { get; set; } = string.Empty;   // N301
    public string AddressLine2 { get; set; } = string.Empty;   // N302

    public static N3Segment Parse(string[] elements)
    {
        return new N3Segment
        {
            RawElements = elements,
            AddressLine1 = elements.ElementAtOrDefault(1) ?? "",
            AddressLine2 = elements.ElementAtOrDefault(2) ?? "",
        };
    }
}

/// <summary>N4 - Geographic Location (City/State/Zip)</summary>
[EdiSegment("N4")]
public class N4Segment : EdiSegmentBase
{
    public string City { get; set; } = string.Empty;            // N401
    public string StateCode { get; set; } = string.Empty;       // N402
    public string PostalCode { get; set; } = string.Empty;      // N403
    public string CountryCode { get; set; } = string.Empty;     // N404

    public static N4Segment Parse(string[] elements)
    {
        return new N4Segment
        {
            RawElements = elements,
            City = elements.ElementAtOrDefault(1) ?? "",
            StateCode = elements.ElementAtOrDefault(2) ?? "",
            PostalCode = elements.ElementAtOrDefault(3) ?? "",
            CountryCode = elements.ElementAtOrDefault(4) ?? "",
        };
    }
}

/// <summary>DTP - Date or Time Period</summary>
[EdiSegment("DTP")]
public class DtpSegment : EdiSegmentBase
{
    public string DateTimeQualifier { get; set; } = string.Empty;   // DTP01 (472=Service, 431=Onset, etc.)
    public string DateTimePeriodFormat { get; set; } = string.Empty; // DTP02 (D8=Date, RD8=Range)
    public string DateTimePeriod { get; set; } = string.Empty;       // DTP03

    public static DtpSegment Parse(string[] elements)
    {
        return new DtpSegment
        {
            RawElements = elements,
            DateTimeQualifier = elements.ElementAtOrDefault(1) ?? "",
            DateTimePeriodFormat = elements.ElementAtOrDefault(2) ?? "",
            DateTimePeriod = elements.ElementAtOrDefault(3) ?? "",
        };
    }
}

/// <summary>PER - Administrative Communications Contact</summary>
[EdiSegment("PER")]
public class PerSegment : EdiSegmentBase
{
    public string ContactFunctionCode { get; set; } = string.Empty;   // PER01
    public string ContactName { get; set; } = string.Empty;           // PER02
    public string CommQualifier1 { get; set; } = string.Empty;        // PER03
    public string CommNumber1 { get; set; } = string.Empty;           // PER04
    public string CommQualifier2 { get; set; } = string.Empty;        // PER05
    public string CommNumber2 { get; set; } = string.Empty;           // PER06

    public static PerSegment Parse(string[] elements)
    {
        return new PerSegment
        {
            RawElements = elements,
            ContactFunctionCode = elements.ElementAtOrDefault(1) ?? "",
            ContactName = elements.ElementAtOrDefault(2) ?? "",
            CommQualifier1 = elements.ElementAtOrDefault(3) ?? "",
            CommNumber1 = elements.ElementAtOrDefault(4) ?? "",
            CommQualifier2 = elements.ElementAtOrDefault(5) ?? "",
            CommNumber2 = elements.ElementAtOrDefault(6) ?? "",
        };
    }
}

/// <summary>DMG - Demographic Information</summary>
[EdiSegment("DMG")]
public class DmgSegment : EdiSegmentBase
{
    public string DateTimePeriodFormat { get; set; } = string.Empty;  // DMG01
    public string DateOfBirth { get; set; } = string.Empty;           // DMG02
    public string GenderCode { get; set; } = string.Empty;            // DMG03

    public static DmgSegment Parse(string[] elements)
    {
        return new DmgSegment
        {
            RawElements = elements,
            DateTimePeriodFormat = elements.ElementAtOrDefault(1) ?? "",
            DateOfBirth = elements.ElementAtOrDefault(2) ?? "",
            GenderCode = elements.ElementAtOrDefault(3) ?? "",
        };
    }
}

/// <summary>HL - Hierarchical Level</summary>
[EdiSegment("HL")]
public class HlSegment : EdiSegmentBase
{
    public string HierarchicalIdNumber { get; set; } = string.Empty;     // HL01
    public string HierarchicalParentId { get; set; } = string.Empty;     // HL02
    public string HierarchicalLevelCode { get; set; } = string.Empty;    // HL03 (20=Info Source, 21=Info Receiver, 22=Subscriber, 23=Dependent)
    public string HierarchicalChildCode { get; set; } = string.Empty;    // HL04 (0=No children, 1=Additional HL)

    public static HlSegment Parse(string[] elements)
    {
        return new HlSegment
        {
            RawElements = elements,
            HierarchicalIdNumber = elements.ElementAtOrDefault(1) ?? "",
            HierarchicalParentId = elements.ElementAtOrDefault(2) ?? "",
            HierarchicalLevelCode = elements.ElementAtOrDefault(3) ?? "",
            HierarchicalChildCode = elements.ElementAtOrDefault(4) ?? "",
        };
    }
}

/// <summary>SBR - Subscriber Information</summary>
[EdiSegment("SBR")]
public class SbrSegment : EdiSegmentBase
{
    public string PayerResponsibilityCode { get; set; } = string.Empty; // SBR01 (P=Primary, S=Secondary, T=Tertiary)
    public string RelationshipCode { get; set; } = string.Empty;        // SBR02
    public string GroupNumber { get; set; } = string.Empty;             // SBR03
    public string GroupName { get; set; } = string.Empty;               // SBR04
    public string InsuranceTypeCode { get; set; } = string.Empty;       // SBR05
    public string ClaimFilingIndicator { get; set; } = string.Empty;    // SBR09

    public static SbrSegment Parse(string[] elements)
    {
        return new SbrSegment
        {
            RawElements = elements,
            PayerResponsibilityCode = elements.ElementAtOrDefault(1) ?? "",
            RelationshipCode = elements.ElementAtOrDefault(2) ?? "",
            GroupNumber = elements.ElementAtOrDefault(3) ?? "",
            GroupName = elements.ElementAtOrDefault(4) ?? "",
            InsuranceTypeCode = elements.ElementAtOrDefault(5) ?? "",
            ClaimFilingIndicator = elements.ElementAtOrDefault(9) ?? "",
        };
    }
}

/// <summary>PRV - Provider Information</summary>
[EdiSegment("PRV")]
public class PrvSegment : EdiSegmentBase
{
    public string ProviderCode { get; set; } = string.Empty;         // PRV01 (BI=Billing, PE=Performing, RF=Referring)
    public string ReferenceIdQualifier { get; set; } = string.Empty; // PRV02
    public string TaxonomyCode { get; set; } = string.Empty;         // PRV03

    public static PrvSegment Parse(string[] elements)
    {
        return new PrvSegment
        {
            RawElements = elements,
            ProviderCode = elements.ElementAtOrDefault(1) ?? "",
            ReferenceIdQualifier = elements.ElementAtOrDefault(2) ?? "",
            TaxonomyCode = elements.ElementAtOrDefault(3) ?? "",
        };
    }
}
