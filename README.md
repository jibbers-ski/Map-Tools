# Jibbers Map Tools

Unity package for creating jibbers maps.

## Installation

### IF YOU HAD THE PREVIOUS TEMPLATE (.zip) DO THIS FIRST:

1. Close Unity
2. **Backup your Project!**
3. Go to your project's `Assets/` folder and delete:
    - `Materials/` folder and `Materials.meta`
    - `Scripts/` folder and `Scripts.meta`
    - `Resources/` folder and `Resources.meta`

### Installing the Package

1. Download the whole project folder (zip or clone)
2. Unpack and drop the `Map-Tools` folder into your project's `Packages/` folder
3. Make sure the folder is called `fun.jibbers.map-tools`
4. Open your project in Unity

**Requires:** Unity 6+, Newtonsoft JSON (`com.unity.nuget.newtonsoft-json`)

## Terrain Editor

### Setup

1. Add a **BetterTerrainEditor** component to a Terrain GameObject
2. The component will automatically read the terrain's heightmap resolution and data
3. Make sure to turn on gizmos in your scene view to be able to see previews

### Shaping a Curve

1. Click on the "+" button under Curve Inserts and expand the new object
2. Select Start/End point of the Curve using the "Pick" buttons and then clicking on the terrain
3. Configure width and Start/End height of the Curve
4. Edit the Animation Curve to change the shape, you can see live changes in the preview
5. Press Apply to shape the terrain along the Curve

## License

MIT