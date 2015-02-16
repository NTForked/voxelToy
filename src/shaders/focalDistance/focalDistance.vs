#version 430

#include <focalDistance/focalDistanceDevice.h>

uniform sampler3D   occupancyTexture;
uniform ivec3       voxelResolution;
uniform vec3        volumeBoundsMin;
uniform vec3        volumeBoundsMax;

uniform vec4        viewport;
uniform float       cameraNear;
uniform float       cameraFar;
uniform mat4        cameraProj;
uniform mat4        cameraInverseProj;
uniform mat4        cameraInverseModelView;
uniform float       cameraFocalLength;
uniform vec2        cameraFilmSize;

uniform sampler2D   backgroundCDFUTexture;
uniform sampler1D   backgroundCDFVTexture;
uniform float	    backgroundIntegral;
uniform float	    backgroundRotationRadians;

uniform vec2        sampledFragment;

#include <shared/constants.h>
#include <shared/aabb.h>
#include <shared/coordinates.h>
#include <shared/dda.h>
#include <shared/sampling.h>
#include <shared/generateRay.h>

// This vertex shader should run for a single vertex, and calculates the
// distance to the closest intersection, in world-space. The distance is then
// fed to another shader down the line to be used as the camera focal distance.

const float Infinity = 99999999.0;	

void main()
{
	vec3 wsRayOrigin;
	vec3 wsRayDir;
	generateRay_Pinhole(vec3(sampledFragment, 0), wsRayOrigin, wsRayDir);
	
	// test intersection with bounds to trivially discard rays before entering
	// traversal.
	float aabbIsectDist = rayAABBIntersection(wsRayOrigin, wsRayDir,
											  volumeBoundsMin, volumeBoundsMax); 
	FocalDistanceData.focalDistance = Infinity;

	if (aabbIsectDist < 0)
	{
		return;
	}

	float rayLength = aabbIsectDist;
	vec3 rayPoint = wsRayOrigin + rayLength * wsRayDir;
	vec3 vsHitPos;

	bool hitGround;
	if ( !traverse(rayPoint, wsRayDir, vsHitPos, hitGround) )
	{
		return;
	}

	// vsHitPos marks the lower-left corner of the voxel. Calculate the
	// precise ray/voxel intersection in world-space
	vec3 wsVoxelSize = (volumeBoundsMax - volumeBoundsMin) / voxelResolution;
	vec3 wsVoxelMin = vsHitPos * wsVoxelSize + volumeBoundsMin; 
	vec3 wsVoxelMax = wsVoxelMin + wsVoxelSize; 
	float voxelHitDistance = rayAABBIntersection(wsRayOrigin, wsRayDir, wsVoxelMin, wsVoxelMax);

	FocalDistanceData.focalDistance = voxelHitDistance;
}
