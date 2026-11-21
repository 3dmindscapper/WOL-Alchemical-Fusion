param([string[]]$Targets)  # each "Type::Method"
$m = "C:\Program Files (x86)\Steam\steamapps\common\Wizard of Legend\WizardOfLegend_Data\Managed"
$null = [AppDomain]::CurrentDomain.add_ReflectionOnlyAssemblyResolve({ param($s,$e) $n = (New-Object Reflection.AssemblyName $e.Name).Name; $p = Join-Path $m "$n.dll"; if (Test-Path $p) { [Reflection.Assembly]::ReflectionOnlyLoadFrom($p) } else { [Reflection.Assembly]::ReflectionOnlyLoad($e.Name) } })
$a = [Reflection.Assembly]::ReflectionOnlyLoadFrom("$m\Assembly-CSharp.dll")
$mod = $a.ManifestModule
$one = @{}; $two = @{}
foreach ($f in [Reflection.Emit.OpCodes].GetFields('Public,Static')) { $op = $f.GetValue($null); if ($op.Size -eq 1) { $one[[int]$op.Value -band 0xff] = $op } else { $two[[int]$op.Value -band 0xff] = $op } }
$bf = [Reflection.BindingFlags]'Public,NonPublic,Static,Instance,DeclaredOnly'
foreach ($tgt in $Targets) {
  $tn, $mn = $tgt -split '::'
  $t = $a.GetType($tn)
  if (-not $t) { "?? type $tn"; continue }
  $methods = @($t.GetMethods($bf) | Where-Object Name -eq $mn) + @(if ($mn -eq '.ctor') { $t.GetConstructors($bf) })
  foreach ($meth in $methods) {
    "===== $tn::$meth"
    $body = $meth.GetMethodBody(); if (-not $body) { "  (no body)"; continue }
    $il = $body.GetILAsByteArray(); $i = 0
    while ($i -lt $il.Length) {
      $pos = $i; $b = $il[$i++]
      if ($b -eq 0xFE) { $op = $two[[int]$il[$i++]] } else { $op = $one[[int]$b] }
      $arg = ''
      switch ($op.OperandType) {
        'InlineNone' {}
        { $_ -in 'ShortInlineBrTarget','ShortInlineI','ShortInlineVar' } { $v = $il[$i]; if ($_ -eq 'ShortInlineBrTarget') { $v = [sbyte]([int]$v - (([int]$v -band 0x80) * 2)); $arg = "-> " + ($i + 1 + $v) } else { $arg = $v }; $i += 1 }
        'InlineVar' { $arg = [BitConverter]::ToUInt16($il, $i); $i += 2 }
        'InlineBrTarget' { $arg = "-> " + ($i + 4 + [BitConverter]::ToInt32($il, $i)); $i += 4 }
        'InlineI' { $arg = [BitConverter]::ToInt32($il, $i); $i += 4 }
        'ShortInlineR' { $arg = [BitConverter]::ToSingle($il, $i); $i += 4 }
        'InlineI8' { $arg = [BitConverter]::ToInt64($il, $i); $i += 8 }
        'InlineR' { $arg = [BitConverter]::ToDouble($il, $i); $i += 8 }
        'InlineSwitch' { $n = [BitConverter]::ToInt32($il, $i); $i += 4 + 4 * $n; $arg = "switch($n)" }
        'InlineString' { $tok = [BitConverter]::ToInt32($il, $i); $i += 4; $arg = '"' + $mod.ResolveString($tok) + '"' }
        default { $tok = [BitConverter]::ToInt32($il, $i); $i += 4
          try { $mem = $mod.ResolveMember($tok); $dt = ''; try { $dt = $mem.DeclaringType.Name } catch {}; $arg = $dt + "::" + $mem.Name } catch { $arg = "tok " + $tok.ToString('x') } }
      }
      "  {0,4}: {1} {2}" -f $pos, $op.Name, $arg
    }
  }
}
