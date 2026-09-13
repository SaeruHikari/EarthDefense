from pathlib import Path
import re
p=Path('tests/SkrGui.Core.Tests/Text/TextBaselineCommonTests.cs');s=p.read_text(encoding='utf-8')
s=re.sub(r'\b(StripCase|BreakCase) (\w+)\[\]\s*\{',r'\1[] \2 = {',s)
# Mask literal strings before recognizing aggregates.
strings=[]
def mask(m):strings.append(m[0]);return 'LIT'+str(len(strings)-1)+'LIT'
s=re.sub(r'"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'',mask,s)
s=re.sub(r'\{\s*(LIT\d+LIT),\s*(LIT\d+LIT),?\s*\}',r'new StripCase(\1,\2)',s)
s=re.sub(r'\{\s*(LIT\d+LIT),\s*([^{}]+),\s*\{\s*((?:new TextRange\([^()]+\)[,\s]*)+)\},?\s*\}',r'new BreakCase(\1,\2,new TextRange[]{\3})',s)
s=re.sub(r'(check_ranges\(\s*ranges,\s*)\{',r'\1new TextRange[]{',s)
s=s.replace('Check.That(fixture.Services);','Check.NotNull(fixture.Services);').replace('Check.That(copy);','Check.NotNull(copy);')
s=s.replace('TextServicesDesc desc{','TextServicesDesc desc = new() {').replace('String(kFamily)','kFamily.ToString()').replace('String(kSource)','kSource.ToString()').replace('style.FontFamilies = kFamily;','style.FontFamilies = kFamily.ToString();')
s=s.replace('.Primary_queries','.PrimaryQueries').replace('.Secondary_queries','.SecondaryQueries').replace('.Primary_loads','.PrimaryLoads').replace('.Secondary_loads','.SecondaryLoads')
s=s.replace('._primary_queries','.PrimaryQueries').replace('._secondary_queries','.SecondaryQueries').replace('._primary_loads','.PrimaryLoads').replace('._secondary_loads','.SecondaryLoads').replace('._query_count','.QueryCount').replace('._load_count','.LoadCount')
for i,v in enumerate(strings):s=s.replace('LIT'+str(i)+'LIT',v)
p.write_text(s,encoding='utf-8')
