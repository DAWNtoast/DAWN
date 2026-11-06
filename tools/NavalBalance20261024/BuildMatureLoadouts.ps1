$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$catalog=@{}; Import-Csv (Join-Path $root 'outputs/component-balance-tuning-20261024/after.csv') | ForEach-Object {$catalog[$_.id]=$_}
$baseIds=@('bow_oak_ram','sail_patched_lateen','cannon_deck_swivel','plating_braced_ribs','instrument_brass_compass')
$sets=@{
 arcane=@(@('bow_voidray_edge','sail_moonweave','cannon_starfire_battery','plating_moonsilver_skin','instrument_lunar_astrolabe'),@('bow_last_horizon','sail_last_free_wind','cannon_worlds_end_volley','plating_worldtree_bark','instrument_last_horizon_lens'))
 curse=@(@('bow_gravecurrent_spur','sail_eclipse_shroud','cannon_voidbell_mortar','plating_eclipse_mail','instrument_eclipse_orrery'),@('bow_crownless_colossus','sail_crown_of_gales','cannon_krakens_thunder','plating_freedom_ark','instrument_eye_of_depths'))
 tide=@(@('bow_abyssal_fang','sail_ghostcurrent_canvas','cannon_deepstar_lance','plating_ghostreef_carapace','instrument_reefstar_astrolabe'),@('bow_leviathan_ram','sail_tempest_wing','cannon_liberty_salute','plating_leviathan_scales','instrument_worldcompass'))
 thunder=@(@('bow_blackreef_hammer','sail_aurora_fan','cannon_sunforge_salvo','plating_aurora_laminate','instrument_aurora_calculator'),@('bow_worldsplitter','sail_skybreaker_array','cannon_stormcrown_broadsides','plating_stormark_bulwark','instrument_stormoracle'))
}
$rows=foreach($original in (Import-Csv (Join-Path $root 'outputs/naval-balance-20261024/calibration-loadouts.csv') | Where-Object {[int]$_.CalibrationTier -ge 3})) {
 foreach($school in @('arcane','curse','tide','thunder')) {
  $row=[ordered]@{};foreach($prop in $original.PSObject.Properties){$row[$prop.Name]=$prop.Value}
  $tier=[int]$original.CalibrationTier
  $ids=if($tier -eq 3){@($original.ComponentIds -split ';')}else{@($sets[$school][$tier-4])}
  $delta=@{hull=0.;armor=0.;firepower=0.;speed=0.};$price=0;$wood=0;$seen=@{}
  for($i=0;$i -lt 5;$i++) {
   $entry=$catalog[$ids[$i]];$baseline=$catalog[$baseIds[$i]]
   if(!$entry -or $entry.slot -ne $baseline.slot -or $seen.ContainsKey($entry.slot)){throw 'Invalid component set'}
   $seen[$entry.slot]=$true
   foreach($a in @('hull','armor','firepower','speed')){$delta[$a]+=[double]$entry.$a-[double]$baseline.$a}
   $price+=[int]$entry.price;$wood+=[int]$entry.installWood
  }
  $row.Id=$original.Id+'-'+$school+'-formed';$row['School']=$school
  foreach($a in @('Hull','Armor','Firepower','Speed')){$row[$a]=[Math]::Round([double]$original.$a-[double]$original.($a+'Delta')+$delta[$a.ToLower()],2);$row[$a+'Delta']=[Math]::Round($delta[$a.ToLower()],2)}
  $row.FullOrdinaryCrewSpeed=[Math]::Round($row.Speed*1.1,2)
  $row.CannonMultiplier=.52+.08*$row.Firepower;$row.ArmorMultiplier=[Math]::Max(.5,12/(10+$row.Armor))
  $row.CannonBase10=[Math]::Round(10*$row.CannonMultiplier,0,[MidpointRounding]::AwayFromZero)
  $row.CannonBase20=[Math]::Round(20*$row.CannonMultiplier,0,[MidpointRounding]::AwayFromZero)
  $row.IncomingRaw20=[Math]::Ceiling(20*$row.ArmorMultiplier);$row.CatalogComponentPrice=$price;$row.InstallWood=$wood
  $row.ComponentIds=$ids -join ';';$row.ComponentNames=($ids | ForEach-Object{$catalog[$_].name}) -join ';'
  $slotNames=@('BowId','SailId','CannonId','PlatingId','InstrumentId');for($i=0;$i -lt 5;$i++){$row[$slotNames[$i]]=$ids[$i]}
  [pscustomobject]$row
 }
}
$rows | Export-Csv (Join-Path $root 'outputs/naval-balance-20261024/mature-loadouts.csv') -NoTypeInformation -Encoding utf8
$rows | Format-Table CalibrationTier,School,Hull,Armor,Firepower,Speed,ComponentIds -AutoSize
