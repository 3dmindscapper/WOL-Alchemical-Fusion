param([string]$OutFile)
# Generates ArcanaCatalog.Generated.cs: every Player arcana (SkillState subclass with a staticID) with family, prefab and prefab kind.
$m = "C:\Program Files (x86)\Steam\steamapps\common\Wizard of Legend\WizardOfLegend_Data\Managed"
$null = [AppDomain]::CurrentDomain.add_ReflectionOnlyAssemblyResolve({ param($s,$e) $n = (New-Object Reflection.AssemblyName $e.Name).Name; $p = Join-Path $m "$n.dll"; if (Test-Path $p) { [Reflection.Assembly]::ReflectionOnlyLoadFrom($p) } else { [Reflection.Assembly]::ReflectionOnlyLoad($e.Name) } })
$a = [Reflection.Assembly]::ReflectionOnlyLoadFrom("$m\Assembly-CSharp.dll")
$mod = $a.ManifestModule
$one = @{}; $two = @{}
foreach ($f in [Reflection.Emit.OpCodes].GetFields('Public,Static')) { $op = $f.GetValue($null); if ($op.Size -eq 1) { $one[[int]$op.Value -band 0xff] = $op } else { $two[[int]$op.Value -band 0xff] = $op } }
$bf = [Reflection.BindingFlags]'Public,NonPublic,Static,Instance,DeclaredOnly'

function Scan($meth, $prefabs, $paths, $strs) {
  $body = $null; try { $body = $meth.GetMethodBody() } catch {}; if (-not $body) { return }
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
      'InlineString' { $sv = $mod.ResolveString([BitConverter]::ToInt32($il, $i)); $i += 4; if ($sv -like 'Assets/Prefabs/*.prefab') { [void]$paths.Add($sv) } else { [void]$strs.Add($sv) } }
      default { $tok = [BitConverter]::ToInt32($il, $i); $i += 4
        if ($op.Name -match 'call') { try { $r = $mod.ResolveMember($tok); if ($r.Name -eq 'get_Prefab') { [void]$prefabs.Add($r.DeclaringType.Name) } } catch {} } }
    }
  }
}
function Chain($t) { $s = @(); while ($t) { try { $s += $t.Name; $t = $t.BaseType } catch { break } }; return $s }
function Kind($cls) {
  $t = $a.GetType($cls); if (-not $t) { return 'Other' }
  $c = Chain $t
  if ($c -contains 'BasicMinion') { return 'Minion' }
  if ($c -contains 'Seeker') { return 'Seeker' }
  if ($c -contains 'Ward') { return 'Ward' }
  if ($c -contains 'Wave') { return 'Wave' }
  if ($c -contains 'Projectile') { return 'Projectile' }
  return 'Other'
}
# prefab classes that are effects/helpers, never the damaging object
$ignore = @('DustEmitter','CastingCircle','PowerUpEffect','SpellCastEffect','PoolManager','Shadow','EffectPrefab')

$rows = New-Object System.Collections.ArrayList
$player = $a.GetType('Player')
foreach ($t in $player.GetNestedTypes($bf)) {
  $chain = Chain $t
  if (-not ($chain -contains 'SkillState') -or $t.Name -eq 'SkillState') { continue }
  $cctor = $t.TypeInitializer; if (-not $cctor) { continue }
  $ids = New-Object System.Collections.ArrayList; $dummyP = New-Object System.Collections.ArrayList; $dummyS = New-Object System.Collections.ArrayList
  Scan $cctor $dummyP $dummyS $ids
  if ($ids.Count -eq 0) { continue }
  $id = $ids[0]
  $family = 'Spell'
  if ($chain -contains 'ProjectileAttackState') { $family = 'Projectile' }
  if ($chain -contains 'BaseDashState') { $family = 'Dash' }
  if ($chain -contains 'BaseJumpState') { $family = 'Jump' }
  if ($chain -contains 'MeleeAttackState') { $family = 'Melee' }
  if ($chain -contains 'UseBaseMinion') { $family = 'Minion' }
  if ($chain -contains 'UseSeekers') { $family = 'Seeker' }
  if ($chain -contains 'BaseWardState') { $family = 'Ward' }
  if ($chain -contains 'UseBuffSkill' -or $t.Name -eq 'HealState') { $family = 'Excluded' }
  $chainShots = $chain -contains 'ProjectileChainAttackState'
  $prefabs = New-Object System.Collections.ArrayList; $paths = New-Object System.Collections.ArrayList; $strs = New-Object System.Collections.ArrayList
  $tt = $t
  while ($tt -and $tt.Name -ne 'SkillState') {
    foreach ($mm in @($tt.GetMethods($bf)) + @($tt.GetConstructors($bf))) { Scan $mm $prefabs $paths $strs }
    foreach ($nt in $tt.GetNestedTypes($bf)) { foreach ($mm in $nt.GetMethods($bf)) { Scan $mm $prefabs $paths $strs } }
    try { $tt = $tt.BaseType } catch { break }
  }
  $pf = @($prefabs | Where-Object { $ignore -notcontains $_ } | Sort-Object -Unique)
  # primary prefab: one sharing a word with the arcana name, else the first
  $words = [regex]::Matches(($id -replace '^(Use|Shoot|Summon|Throw)',''), '[A-Z][a-z]+') | ForEach-Object { $_.Value }
  $primary = $null; $best = 0
  foreach ($p in $pf) {
    $score = 0; foreach ($w in $words) { if ($w.Length -gt 2 -and $p -like "*$w*") { $score++ } }
    if ($score -gt $best) { $best = $score; $primary = $p }
  }
  if (-not $primary -and $pf.Count -gt 0) { $primary = $pf[0] }
  $path = $null; if (-not $primary -and $paths.Count -gt 0) { $path = @($paths | Sort-Object -Unique)[0] }
  $kind = if ($primary) { Kind $primary } else { 'Other' }
  $all = ($pf | ForEach-Object { '"' + $_ + '"' }) -join ', '
  $fan = ($id -match 'Fan|Shotgun|Volley|Barrage|MultiShot|Splinter|Cluster|Seekers')
  $line = '            new GenArcana("{0}", "{1}", ArcanaFamily.{2}, {3}, {4}, PrefabKind.{5}, {6}, {7}, new string[] {{ {8} }}),' -f $id, $t.Name, $family,
    $(if ($primary) { '"' + $primary + '"' } else { 'null' }), $(if ($path) { '"' + $path + '"' } else { 'null' }), $kind,
    $(if ($chainShots) { 'true' } else { 'false' }), $(if ($fan) { 'true' } else { 'false' }), $all
  [void]$rows.Add($line)
}
$header = @"
// <auto-generated> by gencatalog.ps1 from Assembly-CSharp.dll (Wizard of Legend). Do not edit by hand; regenerate. </auto-generated>
// One row per player arcana: staticID, class, family, primary prefab class (static Prefab) or prefab path,
// prefab kind, chain-shot family, fan/volley name, and every prefab class its code references.
namespace WolHybridBosses
{
    internal static partial class ArcanaCatalog
    {
        internal static readonly GenArcana[] Vanilla =
        {
"@
$footer = @"
        };
    }
}
"@
Set-Content -Path $OutFile -Value ($header + "`r`n" + ($rows -join "`r`n") + "`r`n" + $footer) -Encoding utf8
"rows: " + $rows.Count
