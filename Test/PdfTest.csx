// PDFPuzzle 動作確認スクリプト
// dotnet script または dotnet-script で実行

#r "nuget: PDFsharp, 6.2.4"
#r "nuget: System.Security.Cryptography.Pkcs, 10.0.15"

using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfSharp.Drawing;
using System.IO;

var testDir = Path.Combine(Path.GetTempPath(), "PDFPuzzle_Test");
Directory.CreateDirectory(testDir);
Console.WriteLine($"テストフォルダ: {testDir}");

// --- サンプルPDFを2つ作成 ---
string pdf1 = Path.Combine(testDir, "test1.pdf");
string pdf2 = Path.Combine(testDir, "test2.pdf");

void CreateSamplePdf(string path, int pages)
{
    using var doc = new PdfDocument();
    for (int i = 0; i < pages; i++)
    {
        var page = doc.AddPage();
        using var gfx = XGraphics.FromPdfPage(page);
        gfx.DrawRectangle(XBrushes.Black, 20, 20, 20 + 10 * i, 40);
    }
    doc.Save(path);
    Console.WriteLine($"  作成: {Path.GetFileName(path)} ({pages}ページ)");
}

Console.WriteLine("\n[1] サンプルPDF作成");
CreateSamplePdf(pdf1, 3);
CreateSamplePdf(pdf2, 2);

// --- テスト1: 結合 ---
Console.WriteLine("\n[2] 結合テスト (test1 + test2 → merged.pdf)");
string mergedPath = Path.Combine(testDir, "merged.pdf");
{
    using var output = new PdfDocument();
    foreach (var file in new[] { pdf1, pdf2 })
    {
        using var input = PdfReader.Open(file, PdfDocumentOpenMode.Import);
        for (int i = 0; i < input.PageCount; i++)
            output.AddPage(input.Pages[i]);
    }
    output.Save(mergedPath);
    using var verify = PdfReader.Open(mergedPath, PdfDocumentOpenMode.Import);
    Console.WriteLine($"  結果: {verify.PageCount}ページ (期待値: 5) → {(verify.PageCount == 5 ? "✓ OK" : "✗ NG")}");
}

// --- テスト2: 分割 (1ページずつ) ---
Console.WriteLine("\n[3] 分割テスト (merged.pdf → 5つのファイル)");
{
    using var input = PdfReader.Open(mergedPath, PdfDocumentOpenMode.Import);
    int count = 0;
    for (int start = 0; start < input.PageCount; start++)
    {
        using var output = new PdfDocument();
        output.AddPage(input.Pages[start]);
        string outPath = Path.Combine(testDir, $"split_part{start + 1}.pdf");
        output.Save(outPath);
        count++;
    }
    Console.WriteLine($"  結果: {count}ファイル生成 (期待値: 5) → {(count == 5 ? "✓ OK" : "✗ NG")}");
}

// --- テスト3: ページ抽出 (1,3,5ページ) ---
Console.WriteLine("\n[4] ページ抽出テスト (merged.pdf の 1,3,5ページ → extracted.pdf)");
{
    using var input = PdfReader.Open(mergedPath, PdfDocumentOpenMode.Import);
    using var output = new PdfDocument();
    var pages = new[] { 1, 3, 5 };
    foreach (int p in pages)
        output.AddPage(input.Pages[p - 1]);
    string extractedPath = Path.Combine(testDir, "extracted.pdf");
    output.Save(extractedPath);
    using var verify = PdfReader.Open(extractedPath, PdfDocumentOpenMode.Import);
    Console.WriteLine($"  結果: {verify.PageCount}ページ (期待値: 3) → {(verify.PageCount == 3 ? "✓ OK" : "✗ NG")}");
}

// --- ファイルサイズ確認 ---
Console.WriteLine("\n[5] 出力ファイル一覧");
foreach (var f in Directory.GetFiles(testDir, "*.pdf"))
    Console.WriteLine($"  {Path.GetFileName(f),25}  {new FileInfo(f).Length,8:N0} bytes");

Console.WriteLine("\n完了");
