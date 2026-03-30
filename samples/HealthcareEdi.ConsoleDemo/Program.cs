using HealthcareEdi.Core.Parsing;
using HealthcareEdi.Transactions.Claim837.Parsing;

Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
Console.WriteLine("║       Healthcare EDI Parser - 837 Demo                  ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
Console.WriteLine();

// ── 1. Check for file argument ─────────────────────────────────
string filePath;
if (args.Length > 0 && File.Exists(args[0]))
{
    filePath = args[0];
}
else
{
    // Use embedded sample data for demo
    filePath = Path.Combine(Path.GetTempPath(), "demo_837p.x12");
    File.WriteAllText(filePath, SampleData.Sample837P);
    Console.WriteLine($"No file provided. Using embedded sample → {filePath}");
}

Console.WriteLine($"Parsing: {filePath}");
Console.WriteLine(new string('─', 60));

// ── 2. Configure parser ────────────────────────────────────────
var options = new ParserOptions
{
    ValidationMode = ValidationMode.Lenient,  // Production-friendly default
    TrimElementValues = true,
};

var parser = new Claim837Parser(options);

// ── 3. Parse the file ──────────────────────────────────────────
var fileContent = File.ReadAllText(filePath);
var result = parser.ParseFile(fileContent);

// ── 4. Display results ─────────────────────────────────────────
Console.WriteLine($"Parse completed in {result.ParseDurationMs}ms");
Console.WriteLine($"Transactions parsed: {result.Transactions.Count}");
Console.WriteLine($"Failed transactions: {result.FailedTransactions.Count}");
Console.WriteLine();

if (result.InterchangeHeader != null)
{
    Console.WriteLine("── Interchange (ISA) ──────────────────────────────────");
    Console.WriteLine($"  Sender:     {result.InterchangeHeader.SenderId}");
    Console.WriteLine($"  Receiver:   {result.InterchangeHeader.ReceiverId}");
    Console.WriteLine($"  Control #:  {result.InterchangeHeader.InterchangeControlNumber}");
    Console.WriteLine($"  Usage:      {(result.InterchangeHeader.UsageIndicator == "P" ? "Production" : "Test")}");
}

Console.WriteLine();

foreach (var txn in result.Transactions)
{
    Console.WriteLine($"── Transaction: {txn.Variant} (ST Control: {txn.TransactionControlNumber}) ──");
    Console.WriteLine($"  Submitter:        {txn.Submitter.Name.FullName}");
    Console.WriteLine($"  Billing Provider: {txn.BillingProvider.Name.FullName} (NPI: {txn.BillingProviderNpi})");
    Console.WriteLine($"  Subscriber:       {txn.Subscriber.SubscriberName.FullName} (ID: {txn.Subscriber.MemberId})");

    if (txn.Patient != null)
    {
        Console.WriteLine($"  Patient:          {txn.Patient.PatientName.FullName} (DOB: {txn.Patient.Demographics?.DateOfBirth})");
    }

    Console.WriteLine($"  Payer:            {txn.Subscriber.PayerName.FullName}");
    Console.WriteLine($"  Claims:           {txn.ClaimCount}");
    Console.WriteLine($"  Total Charges:    ${txn.TotalCharges:N2}");
    Console.WriteLine();

    foreach (var claim in txn.Claims)
    {
        Console.WriteLine($"  ── Claim: {claim.PatientAccountNumber} ─────────");
        Console.WriteLine($"     Charge:     ${claim.TotalCharge:N2}");
        Console.WriteLine($"     Dx (Princ): {claim.PrincipalDiagnosis?.Code ?? "N/A"}");
        Console.WriteLine($"     Dx (All):   {string.Join(", ", claim.AllDiagnoses.Select(d => d.Code))}");
        Console.WriteLine($"     Facility:   {claim.ClaimInfo.FacilityCodeValue}");
        Console.WriteLine();

        foreach (var line in claim.ServiceLines)
        {
            Console.WriteLine($"     Line {line.LineNumber}: {line.ProcedureCode}  ${line.ChargeAmount:N2}");
            if (line.ProfessionalService != null && line.ProfessionalService.Procedure.Modifiers.Length > 0)
                Console.WriteLine($"       Modifiers: {string.Join(", ", line.ProfessionalService.Procedure.Modifiers)}");
        }

        // Show references
        if (claim.References.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("     References:");
            foreach (var r in claim.References)
                Console.WriteLine($"       {r.ReferenceIdQualifier}: {r.ReferenceId}");
        }
    }

    // Warnings
    if (txn.HasParseWarnings)
    {
        Console.WriteLine();
        Console.WriteLine($"  ⚠ Validation Issues: {txn.ValidationIssues.Count}");
        foreach (var issue in txn.ValidationIssues.Take(5))
            Console.WriteLine($"    {issue}");
    }

    Console.WriteLine();
}

// ── 5. Demo: PHI Redaction ─────────────────────────────────────
Console.WriteLine("── PHI Redaction Demo ──────────────────────────────────");
if (result.Transactions.Count > 0)
{
    var firstTxn = result.Transactions[0];
    Console.WriteLine($"  Before: Patient = {firstTxn.PatientName}");
    Console.WriteLine($"  Before: Member ID = {firstTxn.Subscriber.MemberId}");

    firstTxn.RedactPhi();

    Console.WriteLine($"  After:  Patient = {firstTxn.PatientName}");
    Console.WriteLine($"  After:  Member ID = {firstTxn.Subscriber.SubscriberName.IdCode}");
}

Console.WriteLine();
Console.WriteLine("Done. Press any key to exit.");

// ── Embedded Sample Data ──────────────────────────────────────
static class SampleData
{
    public const string Sample837P =
        "ISA*00*          *00*          *ZZ*CLEARINGHOUSE  *ZZ*PAYERID        *230615*1253*^*00501*000000101*0*P*:~" +
        "GS*HC*CLEARINGHOUSE*PAYERID*20230615*1253*101*X*005010X222A1~" +
        "ST*837*000000001*005010X222A1~" +
        "BHT*0019*00*BATCH12345*20230615*1253*CH~" +
        "NM1*41*2*PREMIER BILLING SERVICES*****46*S12345~" +
        "PER*IC*JANE COORDINATOR*TE*5551234567~" +
        "NM1*40*2*ACME HEALTH PLAN*****46*P98765~" +
        "HL*1**20*1~" +
        "PRV*BI*PXC*207Q00000X~" +
        "NM1*85*1*SMITH*ROBERT*J***XX*1234567890~" +
        "N3*123 MEDICAL CENTER DR*SUITE 400~" +
        "N4*ANYTOWN*NY*12345~" +
        "REF*EI*123456789~" +
        "HL*2*1*22*1~" +
        "SBR*P*18*GRP001*ACME HEALTH PLAN*****CI~" +
        "NM1*IL*1*DOE*JOHN*M***MI*ABC123456789~" +
        "N3*456 OAK STREET~" +
        "N4*ANYTOWN*NY*12345~" +
        "DMG*D8*19850315*M~" +
        "NM1*PR*2*ACME HEALTH PLAN*****PI*ACME001~" +
        "REF*G2*P98765~" +
        "HL*3*2*23*0~" +
        "NM1*QC*1*DOE*JANE*A***MI*ABC123456789~" +
        "N3*456 OAK STREET~" +
        "N4*ANYTOWN*NY*12345~" +
        "DMG*D8*20150722*F~" +
        "CLM*PATIENT-ACCT-001*250***11:B:1*Y*A*Y*Y~" +
        "REF*EA*MEDREC001~" +
        "REF*D9*ORIG-CLM-001~" +
        "HI*ABK:J0610*ABF:M5456*ABF:Z7989~" +
        "DTP*431*D8*20230610~" +
        "DTP*472*D8*20230615~" +
        "NM1*82*1*JOHNSON*EMILY****XX*9876543210~" +
        "LX*1~" +
        "SV1*HC:99213:25*125*UN*1*11**1~" +
        "DTP*472*D8*20230615~" +
        "REF*6R*CHARGE001~" +
        "LX*2~" +
        "SV1*HC:99395*125*UN*1*11**1~" +
        "DTP*472*D8*20230615~" +
        "REF*6R*CHARGE002~" +
        "SE*35*000000001~" +
        "GE*1*101~" +
        "IEA*1*000000101~";
}
