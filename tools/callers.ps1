# Lists methods that call Type::Method: callers.ps1 "MeshGenerator" "GenerateMeshes"
param([string]$Type, [string]$Method)
$m = "C:\Program Files (x86)\Steam\steamapps\common\Wizard of Legend\WizardOfLegend_Data\Managed"
$null = [AppDomain]::CurrentDomain.add_ReflectionOnlyAssemblyResolve({ param($s,$e) $n = (New-Object Reflection.AssemblyName $e.Name).Name; $p = Join-Path $m "$n.dll"; if (Test-Path $p) { [Reflection.Assembly]::ReflectionOnlyLoadFrom($p) } else { [Reflection.Assembly]::ReflectionOnlyLoad($e.Name) } })
$a = [Reflection.Assembly]::ReflectionOnlyLoadFrom("$m\Assembly-CSharp.dll")
$bf = [Reflection.BindingFlags]'Public,NonPublic,Instance,Static,DeclaredOnly'
try { $ts = $a.GetTypes() } catch { $ts = $_.Exception.Types | Where-Object { $_ } }
$toks = @($a.GetType($Type).GetMethods($bf) | Where-Object Name -eq $Method | ForEach-Object { $_.MetadataToken })
$found = New-Object System.Collections.Generic.HashSet[string]
foreach ($t in $ts) {
  foreach ($me in @($t.GetMethods($bf)) + @($t.GetConstructors($bf))) {
    $b = $null; try { $b = $me.GetMethodBody() } catch {}
    if (-not $b) { continue }
    $il = $b.GetILAsByteArray()
    for ($i = 0; $i -lt $il.Length - 4; $i++) {
      if (($il[$i] -eq 0x28 -or $il[$i] -eq 0x6F) -and ($toks -contains [BitConverter]::ToInt32($il, $i + 1))) { [void]$found.Add("$($t.FullName)::$($me.Name)"); break }
    }
  }
}
$found | Sort-Object
