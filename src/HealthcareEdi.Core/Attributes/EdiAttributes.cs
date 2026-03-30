namespace HealthcareEdi.Core.Attributes;

/// <summary>
/// Marks a class as representing an EDI segment and specifies its segment identifier.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class EdiSegmentAttribute : Attribute
{
    /// <summary>
    /// The segment identifier (e.g., "NM1", "CLM", "REF").
    /// </summary>
    public string SegmentId { get; }

    public EdiSegmentAttribute(string segmentId)
    {
        SegmentId = segmentId;
    }
}

/// <summary>
/// Marks a class as representing an EDI loop with its loop identifier.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class EdiLoopAttribute : Attribute
{
    /// <summary>
    /// The loop identifier (e.g., "2000A", "2300", "2400").
    /// </summary>
    public string LoopId { get; }

    public EdiLoopAttribute(string loopId)
    {
        LoopId = loopId;
    }
}
