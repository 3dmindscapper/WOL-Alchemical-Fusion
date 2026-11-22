# Lists methods that load one of the given string literals: strings.ps1 "Meshes","Walls"
param([string[]]$Values)
$m = "C:\Program Files (x86)\Steam\steamapps\common\Wizard of Legend\WizardOfLegend_Data\Managed"
$null = [AppDomain]::CurrentDomain.add_ReflectionOnlyAssemblyResolve({ param($s,$e) $n = (New-Object Reflection.AssemblyName $e.Name).Name; $p = Join-Path $m "$n.dll"; if (Test-Path $p) { [Reflection.Assembly]::ReflectionOnlyLoadFrom($p) } else { [Reflection.Assembly]::ReflectionOnlyLoad($e.Name) } })
$a = [Reflection.Assembly]::ReflectionOnlyLoadFrom("$m\Assembly-CSharp.dll")
$mod = $a.ManifestModule
$bf = [Reflection.BindingFlags]'Public,NonPublic,Instance,Static,DeclaredOnly'
try { $ts = $a.GetTypes() } catch { $ts = $_.Exception.Types | Where-Object { $_ } }
$found = New-Object System.Collections.Generic.HashSet[string]
foreach ($t in $ts) {
  foreach ($me in @($t.GetMethods($bf)) + @($t.GetConstructors($bf))) {
    $b = $null; try { $b = $me.GetMethodBody() } catch {}
    if (-not $b) { continue }
    $il = $b.GetILAsByteArray()
    for ($i = 0; $i -lt $il.Length - 4; $i++) {
      if ($il[$i] -ne 0x72) { continue }
      $s = $null; try { $s = $mod.ResolveString([BitConverter]::ToInt32($il, $i + 1)) } catch { continue }
      if ($Values -contains $s) { [void]$found.Add("$($t.FullName)::$($me.Name) -> `"$s`"") }
    }
  }
}
$found | Sort-Object
