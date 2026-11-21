param([string[]]$Classes)
# Flags per prefab class: SPAWNS (Instantiate/ChaosInst/other Prefab), SETINFO (SetAttackInfo), TARGETS (enemy lookups), PLAYERREF
$m = "C:\Program Files (x86)\Steam\steamapps\common\Wizard of Legend\WizardOfLegend_Data\Managed"
$null = [AppDomain]::CurrentDomain.add_ReflectionOnlyAssemblyResolve({ param($s,$e) $n = (New-Object Reflection.AssemblyName $e.Name).Name; $p = Join-Path $m "$n.dll"; if (Test-Path $p) { [Reflection.Assembly]::ReflectionOnlyLoadFrom($p) } else { [Reflection.Assembly]::ReflectionOnlyLoad($e.Name) } })
$a = [Reflection.Assembly]::ReflectionOnlyLoadFrom("$m\Assembly-CSharp.dll")
$mod = $a.ManifestModule
$one = @{}; $two = @{}
foreach ($f in [Reflection.Emit.OpCodes].GetFields('Public,Static')) { $op = $f.GetValue($null); if ($op.Size -eq 1) { $one[[int]$op.Value -band 0xff] = $op } else { $two[[int]$op.Value -band 0xff] = $op } }
$bf = [Reflection.BindingFlags]'Public,NonPublic,Static,Instance,DeclaredOnly'
foreach ($cn in $Classes) {
  $t = $a.GetType($cn); if (-not $t) { "$cn`t??"; continue }
  $flags = @{}
  $ms = @(); $tt = $t
  while ($tt -and $tt.Name -ne 'MonoBehaviour' -and $tt.Name -ne 'Projectile') { try { $ms += $tt.GetMethods($bf); foreach ($nt in $tt.GetNestedTypes($bf)) { $ms += $nt.GetMethods($bf) }; $tt = $tt.BaseType } catch { break } }
  foreach ($meth in $ms) {
    $body = $null; try { $body = $meth.GetMethodBody() } catch {}; if (-not $body) { continue }
    $il = $body.GetILAsByteArray(); $i = 0
    while ($i -lt $il.Length) {
      $b = $il[$i++]; if ($b -eq 0xFE) { $op = $two[[int]$il[$i++]] } else { $op = $one[[int]$b] }
      if (-not $op) { break }
      switch ($op.OperandType) {
        'InlineNone' {}
        { $_ -in 'ShortInlineBrTarget','ShortInlineI','ShortInlineVar' } { $i += 1 }
        'InlineVar' { $i += 2 }
        { $_ -in 'InlineI8','InlineR' } { $i += 8 }
        'InlineSwitch' { $n = [BitConverter]::ToInt32($il, $i); $i += 4 + 4 * $n }
        'InlineString' { $i += 4 }
        default { $tok = [BitConverter]::ToInt32($il, $i); $i += 4
          try { $r = $mod.ResolveMember($tok); $nm = $r.Name; $dt = ''; try { $dt = $r.DeclaringType.Name } catch {}
            if ($nm -match '^(Instantiate|ChaosInst|CreateProjectile)$') { $flags['SPAWNS'] = 1 }
            if ($nm -eq 'get_Prefab' -and $dt -ne $cn) { $flags['SPAWNS:' + $dt] = 1 }
            if ($nm -eq 'SetAttackInfo') { $flags['SETINFO'] = 1 }
            if ($nm -match 'GetClosestTarget|OverlapCircle|FindObjectsOfType' -or ($dt -eq 'GameController' -and $nm -match 'enemies|players|allies')) { $flags['TARGETS:' + $nm] = 1 }
            if ($dt -eq 'Player' -or $nm -match 'playerScripts|activePlayers') { $flags['PLAYERREF'] = 1 }
          } catch {} }
      }
    }
  }
  "{0}`t{1}" -f $cn, (($flags.Keys | Sort-Object) -join ' ')
}
