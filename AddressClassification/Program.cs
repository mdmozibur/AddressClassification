//Load sample data
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using static AddressClassification.AddressClassifier;

const string prompt = "Enter the file name with full path : ";
const string emptyFileName = "Provided file name was empty. Please try again...";

Console.Write(prompt);
var fileName = Console.ReadLine();
int processedRow = 0;   
System.Timers.Timer Timer = new(1000);
Timer.Elapsed += Timer_Elapsed;

void Timer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
{
    if (processedRow == 0)
        return;
    Console.Write($"\r Processed {processedRow} row(s)...");
}

while (true)
{
    if (string.IsNullOrWhiteSpace(fileName))
    {
        Console.WriteLine(emptyFileName);
        continue;
    }
    break;
}

FileStream file = null;
SpreadsheetDocument reader = null;
Worksheet? sheet = null;
SharedStringTable? sst = null;
Console.WriteLine("Opening spreadsheet...");

try
{
    file = File.Open(fileName, FileMode.Open, FileAccess.ReadWrite);
    reader = SpreadsheetDocument.Open(file, true);
    var tmp3 = reader.WorkbookPart.Workbook.Descendants<Sheet>().Where(s => s.Name == "Query result").FirstOrDefault();
    var tmp = reader.WorkbookPart.WorksheetParts.Where(wsp => wsp.Worksheet.XName is not null);
    WorkbookPart workbookPart = reader.WorkbookPart;
    sheet = (workbookPart.GetPartById(tmp3.Id) as WorksheetPart).Worksheet;
    SharedStringTablePart sstpart = workbookPart.GetPartsOfType<SharedStringTablePart>().First();
    sst = sstpart.SharedStringTable;

    if (sheet is not null)
    {
        Timer.Start();
    }
}
catch (Exception exc)
{
    Console.WriteLine(exc.Message);
}

await Task.Run(() =>
{
    int iteratedRow = 0;
    Console.WriteLine("Processing started...");
    foreach (var row in sheet.Descendants<Row>())
    {
        if (row is null)
            continue;

        if (row.RowIndex.Value == 1)
            continue;

        var cells = row.Descendants<Cell>().Where(c => c.CellReference.Value.StartsWith('B') || c.CellReference.Value.StartsWith('C') || c.CellReference.Value.StartsWith('D'));
        var address = cells.Where(c => c.CellReference.Value.StartsWith('B')).FirstOrDefault();
        var last_Cell = cells.Where(c => c.CellReference.Value.StartsWith('D')).FirstOrDefault();

        iteratedRow++;

        if (address?.CellValue is null && address.DataType != CellValues.InlineString)
            continue;

        string addVal;
        if (address.CellValue is null)
            addVal = address.InnerText;
        else
        {
            int ssid = int.Parse(address?.CellValue?.Text ?? address.InnerText);
            addVal = sst.ChildElements[ssid].InnerText;
        }

        ModelInput sampleData = new()
        {
            DELIVERY_ADDRESS = addVal,
        };


        var predictionResult = Predict(sampleData);
        if (predictionResult.Score.Max() < 0.8)
            continue;

        var cell = new Cell
        {
            CellReference = 'E' + row.RowIndex.ToString(),
            CellValue = new CellValue(predictionResult.PredictedLabel),
            DataType = CellValues.String
        };
        var prevCell = row.InsertAfter(cell, last_Cell);

        var cell2 = new Cell
        {
            CellReference = 'F' + row.RowIndex.ToString(),
            CellValue = new CellValue("ProgrammaticV2"),
            DataType = CellValues.String
        };
        row?.InsertAfter(cell2, prevCell);
        ++processedRow;
        //if (++processedRow > 500)
        //    break;
    }
    sheet.Save();
});
reader?.Close();
await file?.FlushAsync();
