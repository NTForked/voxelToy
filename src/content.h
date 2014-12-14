#pragma once

#include "noise.h"

#include <QOpenGLFunctions>
#include <OpenEXR/ImathMatrix.h>
#include <OpenEXR/ImathBox.h>

struct RGB
{
	GLubyte r, g, b;
	RGB(GLubyte r, GLubyte g, GLubyte b)
	{
		this->r = r;
		this->g = g;
		this->b = b;
	}
	RGB(const RGB& other)
	{
		r = other.r;
		g = other.g;
		b = other.b;
	}
};

namespace VoxelTools
{

void addVoxelSphere( const Imath::V3f sphereCenter, float sphereRadius, 
                     const Imath::V3i volumeResolution,
					 const Imath::Box3f volumeBounds,
					 GLubyte* occupancyTexels, RGB* colorTexels);

void addPlane( const Imath::V3f& normal, const Imath::V3f& p,
			   const Imath::V3i volumeResolution,
			   const Imath::Box3f volumeBounds,
			   GLubyte* occupancyTexels, RGB* colorTexels );

} // namespace voxelTools
