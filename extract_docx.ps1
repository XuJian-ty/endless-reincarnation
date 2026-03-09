$docxPath = "c:\Users\xujia\OneDrive\Desktop\你参考我的这个最新要求.docx"
$outPath = "d:\Users\unity\3D肉鸽\docx_text.txt"

if (-not (Test-Path $docxPath)) {
    "FILE_NOT_FOUND: $docxPath" | Out-File $outPath -Encoding utf8
    exit 1
}

$zip = [System.IO.Compression.ZipFile]::OpenRead($docxPath)
$entry = $zip.GetEntry("word/document.xml")
if (-not $entry) {
    "NO_DOCUMENT_XML" | Out-File $outPath -Encoding utf8
    $zip.Dispose()
    exit 1
}

$stream = $entry.Open()
$reader = New-Object System.IO.StreamReader($stream)
$xml = $reader.ReadToEnd()
$reader.Close()
$stream.Close()
$zip.Dispose()

# Simple tag strip: paragraphs to newline, then remove all tags
$text = $xml -replace '<w:p [^>]*>', "`n" -replace '<[^>]+>', ' '
$text = $text -replace '&amp;', '&' -replace '&lt;', '<' -replace '&gt;', '>' -replace '&quot;', '"'
$text = ($text -split "`n" | ForEach-Object { ($_ -replace '\s+', ' ').Trim() }) -join "`n"
$text.Trim() | Out-File $outPath -Encoding utf8
