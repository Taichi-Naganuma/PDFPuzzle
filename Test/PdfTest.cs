using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using PdfSharpCore.Drawing;
using System.IO;

var testDir = Path.Combine(Path.GetTempPath(), "PDFPuzzle_Test");
Directory.CreateDirectory(testDir);
Console.WriteLine($"テストフォルダ: {testDir}\n");

int pass = 0, fail = 0;

void Check(string label, bool condition)
{
    if (condition) { Console.WriteLine($"  ✓ {label}"); pass++; }
    else           { Console.WriteLine($"  ✗ {label}"); fail++; }
}

// サンプルPDF作成
string pdf1 = Path.Combine(testDir, "test1.pdf");
string pdf2 = Path.Combine(testDir, "test2.pdf");

void CreatePdf(string path, int pages)
{
    var doc = new PdfDocument();
    for (int i = 0; i < pages; i++)
    {
        var page = doc.AddPage();
        var gfx = XGraphics.FromPdfPage(page);
        gfx.DrawString($"Page {i+1} / {Path.GetFileName(path)}",
            new XFont("Arial", 18), XBrushes.Black,
            new XRect(0, 0, page.Width, page.Height), XStringFormats.Center);
    }
    doc.Save(path);
}

Console.WriteLine("[1] サンプルPDF作成");
CreatePdf(pdf1, 3);
CreatePdf(pdf2, 2);
Check("test1.pdf 3ページ", PdfReader.Open(pdf1, PdfDocumentOpenMode.Import).PageCount == 3);
Check("test2.pdf 2ページ", PdfReader.Open(pdf2, PdfDocumentOpenMode.Import).PageCount == 2);

// テスト: 結合
Console.WriteLine("\n[2] 結合テスト");
string merged = Path.Combine(testDir, "merged.pdf");
{
    using var output = new PdfDocument();
    foreach (var f in new[] { pdf1, pdf2 })
    {
        using var input = PdfReader.Open(f, PdfDocumentOpenMode.Import);
        for (int i = 0; i < input.PageCount; i++) output.AddPage(input.Pages[i]);
    }
    output.Save(merged);
    using var v = PdfReader.Open(merged, PdfDocumentOpenMode.Import);
    Check($"merged.pdf = 5ページ (実際: {v.PageCount})", v.PageCount == 5);
}

// テスト: 分割
Console.WriteLine("\n[3] 分割テスト (1ページずつ)");
{
    using var input = PdfReader.Open(merged, PdfDocumentOpenMode.Import);
    int count = 0;
    for (int s = 0; s < input.PageCount; s++)
    {
        using var output = new PdfDocument();
        output.AddPage(input.Pages[s]);
        output.Save(Path.Combine(testDir, $"split_{s+1}.pdf"));
        count++;
    }
    Check($"{count}ファイル生成 (期待値: 5)", count == 5);
    for (int i = 1; i <= 5; i++)
        Check($"split_{i}.pdf 存在", File.Exists(Path.Combine(testDir, $"split_{i}.pdf")));
}

// テスト: ページ抽出
Console.WriteLine("\n[4] ページ抽出テスト (1,3,5ページ)");
{
    using var input = PdfReader.Open(merged, PdfDocumentOpenMode.Import);
    using var output = new PdfDocument();
    foreach (int p in new[] { 1, 3, 5 }) output.AddPage(input.Pages[p - 1]);
    string extracted = Path.Combine(testDir, "extracted.pdf");
    output.Save(extracted);
    using var v = PdfReader.Open(extracted, PdfDocumentOpenMode.Import);
    Check($"extracted.pdf = 3ページ (実際: {v.PageCount})", v.PageCount == 3);
}

// テスト: 上書き防止（連番）
Console.WriteLine("\n[5] ファイル名連番テスト");
{
    string GetUnique(string path)
    {
        if (!File.Exists(path)) return path;
        string dir = Path.GetDirectoryName(path)!;
        string name = Path.GetFileNameWithoutExtension(path);
        string ext = Path.GetExtension(path);
        int i = 1; string c;
        do { c = Path.Combine(dir, $"{name} ({i++}){ext}"); } while (File.Exists(c));
        return c;
    }
    string base_ = Path.Combine(testDir, "dup.pdf");
    File.WriteAllText(base_, "x");
    string next = GetUnique(base_);
    Check("既存ファイルに連番付与", next.EndsWith("dup (1).pdf"));
}

Console.WriteLine($"\n結果: {pass} passed / {fail} failed");
return fail == 0 ? 0 : 1;
