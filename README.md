# Jibbers Map Tools

Unity package for creating jibbers maps.

## Installation

**Requires:** Unity 6+ with URP

- **Package Manager:** Window > Package Manager > + > Add package from git URL, paste this repository's URL
- **Manually:** download or clone this repository into your project's `Packages/` folder and name the folder `fun.jibbers.map-tools`

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