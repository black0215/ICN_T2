using System.Text.Json.Serialization;

namespace ICN_T2.Tools.ReverseEngineering;

/// <summary>
/// CEC-related callsite evidence extracted from static analysis.
/// </summary>
public sealed record CecCallSite(
    long Address,
    string ServiceName,
    uint? IpcHeader,
    int XrefCount,
    string Notes);

/// <summary>
/// High-level StreetPass encounter path reconstructed from anchors.
/// </summary>
public sealed record StreetPassEncounterPath(
    string RecvFunc,
    string SeedFunc,
    string TableSelectFunc,
    string VipJudgeFunc);

/// <summary>
/// Generic evidence row used in reports.
/// </summary>
public sealed record EncounterEvidenceRow(
    string SourceFile,
    long Offset,
    string ValueHex,
    string InterpretedRole,
    double Confidence);

/// <summary>
/// Save diff candidate entry used for A/B/C/D state comparisons.
/// </summary>
public sealed record SaveStateCandidate(
    int Offset,
    int Width,
    string Before,
    string After,
    [property: JsonPropertyName("class")] string ClassTag);
