$version = "1.2.0"
$buildnumber = $(svnversion) -replace "^.*?\:?(\d+)\D?$", '$1'
$assemblyinfo = gc .\Properties\AssemblyInfo.cs -encoding UTF8
if($buildnumber -ne '')
{
	$assemblyinfo = $assemblyinfo -replace "Version\("".*?""\)", "Version(""$version.$buildnumber"")"
	sc .\Properties\AssemblyInfo.cs $assemblyinfo -encoding UTF8
}
