$textures = @(
    "flashLightMK3.png",
    "miniaturizorMK6_icon.png",
    "miniaturizorMK6.png",
    "betterPotionHpRegen.png",
    "defenseShieldMK2.png",
    "waterBreatherMK2.png",
    "jetpackMK2.png",
    "antiGravityWall.png",
    "turretReparatorMK3_unit.png",
    "turretReparatorMK3_icon.png",
    "turretReparatorMK3.png",
    "megaExplosive.png",
    "turretParticlesMK2_icon.png",
    "turretParticlesMK2_unit.png",
    "turretTeslaMK2.png",
    "collector.png",
    "collector_icon.png",
    "collector_unit.png",
    "blueLightSticky.png",
    "blueLightSticky_midair.png",
    "redLightSticky.png",
    "redLightSticky_midair.png",
    "greenLightSticky.png",
    "greenLightSticky_midair.png",
    "basaltCollector_icon.png",
    "basaltCollector_unit.png",
    "turretLaser360_icon.png",
    "gunPlasmaMegaSnipe.png",
    "gunPlasmaMegaSnipe_icon.png",
    "volcanicExplosive.png",
    "wallCompositeReinforced.png",
    "gunNukeLauncher.png",
    "gunNukeLauncher_icon.png",
    "generatorSunMK2.png",
    "RTG.png",
    "gunPlasmaThrower.png",
    "gunPlasmaThrower_icon.png"
)
$sprites = @(
    "$PSScriptRoot/textures/meltdownSnipe.png"
    "$PSScriptRoot/textures/particlesSnipTurretMK2.png"
)

magick -background none $($textures.foreach({"$PSScriptRoot/textures/$_"})) +append $sprites -append "$PSScriptRoot/textures/combined_textures.png"
