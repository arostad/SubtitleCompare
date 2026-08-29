namespace SubtitleCompare.Core.Ocr;

public readonly record struct OcrProgress(int Current, int Total, string Message);
