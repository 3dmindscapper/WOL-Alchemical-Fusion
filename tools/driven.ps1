# Lists player skill states that drive their spawned object after creating it: calls (or field writes) on
# a prefab class that isn't a plain engine/base type. Those arcana freeze when only the object is spawned.
$m = "C:\Program Files (x86)\Steam\steamapps\common\Wizard of Legend\WizardOfLegend_Data\Managed"
$null = [AppDomain]::CurrentDomain.add_ReflectionOnlyAssemblyResolve({ param($s,$e) $n = (New-Object Reflection.AssemblyName $e.Name).Name; $p = Join-Path $m "$n.dll"; if (Test-Path $p) { [Reflection.Assembly]::ReflectionOnlyLoadFrom($p) } else { [Reflection.Assembly]::ReflectionOnlyLoad($e.Name) } })
$a = [Reflection.Assembly]::ReflectionOnlyLoadFrom("$m\Assembly-CSharp.dll")
$mod = $a.ManifestModule
$one = @{}; $two = @{}
foreach ($f in [Reflection.Emit.OpCodes].GetFields('Public,Static')) { $op = $f.GetValue($null); if ($op.Size -eq 1) { $one[[int]$op.Value -band 0xff] = $op } else { $two[[int]$op.Value -band 0xff] = $op } }
$bf = [Reflection.BindingFlags]'Public,NonPublic,Static,Instance,DeclaredOnly'
$skip = 'Projectile','Component','Transform','GameObject','Object','Behaviour','MonoBehaviour','Attack','Entity','Player','SkillState','State`1','Animator','Vector2','Vector3','Quaternion','Mathf','Globals','ChaosStopwatch','ChaosQuickStopwatch','SoundManager','PoolManager','String','Random','Physics2D','Time','Debug','ProjectileAttackState','CameraController','Health','Movement','FSM','AnimationExtension','List`1','Dictionary`2','ParticleEffect','Collider2D','Rigidbody2D','SpriteRenderer','Renderer','TextManager','StatManager','StatData','AttackInfo','LootManager','GameController','Inventory','Color','Input','Math','Array','Enumerable','ChaosBundle','EnemyArcana','Single','Int32','Boolean'
$player = $a.GetType('Player')
$out = @{}
foreach ($t in $player.GetNestedTypes('Public,NonPublic')) {
  $hits = New-Object System.Collections.Generic.HashSet[string]
  foreach ($meth in $t.GetMethods($bf)) {
    if ($meth.Name -notin 'ExecuteSkill','FixedUpdate','Update','OnExit','OnEnter' -and $meth.Name -notmatch 'Throw|Release|Launch|Recall|Fire|Spin|Init|Create') { continue }
    $body = $meth.GetMethodBody(); if (-not $body) { continue }
    $il = $body.GetILAsByteArray(); $i = 0
    while ($i -lt $il.Length) {
      $b = $il[$i++]
      if ($b -eq 0xFE) { $op = $two[[int]$il[$i++]] } else { $op = $one[[int]$b] }
      switch ($op.OperandType) {
        'InlineNone' {}
        { $_ -in 'ShortInlineBrTarget','ShortInlineI','ShortInlineVar' } { $i += 1 }
        'InlineVar' { $i += 2 }
        { $_ -in 'InlineBrTarget','InlineI','ShortInlineR','InlineString' } { $i += 4 }
        { $_ -in 'InlineI8','InlineR' } { $i += 8 }
        'InlineSwitch' { $n = [BitConverter]::ToInt32($il, $i); $i += 4 + 4 * $n }
        default {
          $tok = [BitConverter]::ToInt32($il, $i); $i += 4
          if ($op.Name -in 'call','callvirt','stfld') {
            try { $mem = $mod.ResolveMember($tok); $dt = $mem.DeclaringType.Name
              if ($dt -and $dt -notin $skip -and $mem.DeclaringType.DeclaringType -eq $null -and $mem.Name -ne 'get_Prefab' -and $mem.Name -notlike 'get_*') { [void]$hits.Add("$dt.$($mem.Name)") } } catch {}
          }
        }
      }
    }
  }
  if ($hits.Count) { "{0,-28} {1}" -f $t.Name, (($hits | Sort-Object) -join ', ') }
}
