using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
namespace Earthward.Domain;

/// <summary>Strict RFC4180 CSV with UTF8 BOM support, physical source lines and invariant typed cells.</summary>
public sealed class CsvTable
{
    public IReadOnlyList<string> Headers { get; }
    public IReadOnlyList<CsvRow> Rows { get; }
    public string SourceName { get; }
    private CsvTable(string source, List<string> headers, List<CsvRow> rows) { SourceName=source;Headers=headers.AsReadOnly();Rows=rows.AsReadOnly(); }
    public static CsvTable Parse(string text,string sourceName="CSV")
    {
        if(text.Length>32*1024*1024)throw new InvalidDataException(sourceName+": table exceeds32MiB");
        if(text.StartsWith('\uFEFF'))text=text[1..];
        var records=new List<(int Line,List<string> Cells)>();var cells=new List<string>();var cell=new StringBuilder();
        int line=1,start=1;bool quoted=false,closed=false,started=false;
        void EndCell(){cells.Add(cell.ToString());cell.Clear();closed=false;started=false;}
        void EndRow(){EndCell();records.Add((start,cells));cells=new();start=line+1;}
        for(int i=0;i<text.Length;i++)
        {
            char c=text[i];
            if(quoted)
            {
                if(c=='"'){if(i+1<text.Length&&text[i+1]=='"'){cell.Append('"');i++;}else{quoted=false;closed=true;}}
                else{cell.Append(c);if(c=='\n'||c=='\r'&&(i+1==text.Length||text[i+1]!='\n'))line++;}
                continue;
            }
            if(c==','){EndCell();continue;}
            if(c=='\r'||c=='\n'){EndRow();if(c=='\r'&&i+1<text.Length&&text[i+1]=='\n')i++;line++;continue;}
            if(closed)throw new InvalidDataException($"{sourceName}:{line}: unexpected character after closing quote");
            if(c=='"'){if(started||cell.Length>0)throw new InvalidDataException($"{sourceName}:{line}: quote inside unquoted field");quoted=true;started=true;continue;}
            cell.Append(c);started=true;
        }
        if(quoted)throw new InvalidDataException($"{sourceName}:{line}: unterminated quoted field");
        if(started||closed||cell.Length>0||cells.Count>0){EndCell();records.Add((start,cells));}
        if(records.Count==0)throw new InvalidDataException(sourceName+": missing header");
        var header=records[0].Cells;
        if(header.Any(string.IsNullOrWhiteSpace)||header.Distinct(StringComparer.Ordinal).Count()!=header.Count)throw new InvalidDataException(sourceName+":1: empty or duplicate column name");
        var rows=new List<CsvRow>();
        foreach(var record in records.Skip(1))
        {
            if(record.Cells.Count!=header.Count)throw new InvalidDataException($"{sourceName}:{record.Line}: expected{header.Count} columns, got{record.Cells.Count}");
            rows.Add(new CsvRow(sourceName,record.Line,header,record.Cells));
        }
        return new CsvTable(sourceName,header,rows);
    }
    public void RequireHeaders(params string[] required)
    {
        foreach(string column in required)if(!Headers.Contains(column))throw new InvalidDataException($"{SourceName}:1: missing column '{column}'");
    }
}
public sealed class CsvRow
{
    private readonly IReadOnlyDictionary<string,string> _cells;
    public string SourceName { get; }
    public int Line { get; }
    internal CsvRow(string source,int line,IReadOnlyList<string> headers,IReadOnlyList<string> cells)
    { SourceName=source;Line=line;_cells=headers.Select((h,i)=>(h,cells[i])).ToDictionary(p=>p.h,p=>p.Item2,StringComparer.Ordinal); }
    public InvalidDataException Error(string column,string reason)=>new($"{SourceName}:{Line}: '{column}': {reason}");
    public bool Has(string column)=>_cells.ContainsKey(column);
    public string String(string column)=>_cells.TryGetValue(column,out string? value)?value:throw Error(column,"missing column");
    public string OptionalString(string column,string fallback="")=>String(column) is {Length:>0} text?text:fallback;
    public double Number(string column)
    { string s=String(column);if(!double.TryParse(s,NumberStyles.Float,CultureInfo.InvariantCulture,out double n)||!double.IsFinite(n))throw Error(column,"expected a finite invariant number");return n; }
    public double OptionalNumber(string column,double fallback=0)=>String(column).Length==0?fallback:Number(column);
    public long Integer(string column)
    { if(!long.TryParse(String(column),NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out long n))throw Error(column,"expected an integer");return n; }
    public long OptionalInteger(string column,long fallback=0)=>String(column).Length==0?fallback:Integer(column);
    public bool Boolean(string column)=>String(column) switch {"true"=>true,"false"=>false,_=>throw Error(column,"expected true or false")};
    public bool OptionalBoolean(string column,bool fallback=false)=>String(column).Length==0?fallback:Boolean(column);
}
