﻿using System;
using System.Collections.Generic;
using System.Text;
using ISAAR.MSolve.FEM.Entities;
using ISAAR.MSolve.Geometry.Coordinates;

//TODO: abstract this in order to be used with points in various coordinate systems
//TODO: perhaps the origin should be (0.0, 0.0) and the meshes could then be transformed. Abaqus does something similar with its
//      meshed parts during assembly
namespace ISAAR.MSolve.FEM.Meshes.Custom
{
    /// <summary>
    /// Creates meshes based on uniform rectilinear grids: the distance between two consecutive vertices for the same axis is 
    /// constant. This distance may be different for each axis though. For now the cells are quadrilateral with 4 vertices 
    /// (rectangles in particular).
    /// Authors: Serafeim Bakalakos
    /// </summary>
    public class UniformMeshGenerator : IMeshGenerator2D<Node2D>
    {
        private readonly double minX;
        private readonly double minY;
        private readonly double dx;
        private readonly double dy;
        private readonly int vertexRows, vertexColumns, cellRows, cellColumns;

        public UniformMeshGenerator(double minX, double minY, double maxX, double maxY, int cellsPerX, int cellsPerY)
        {
            this.minX = minX;
            this.minY = minY;
            this.dx = (maxX - minX) / cellsPerX;
            this.dy = (maxY - minY) / cellsPerY;
            this.cellRows = cellsPerY;
            this.cellColumns = cellsPerX;
            this.vertexRows = cellRows + 1;
            this.vertexColumns = cellColumns + 1;
        }

        public (Node2D[] vertices, CellType2D[] cellTypes, Node2D[][] cellConnectivities) CreateMesh()
        {
            Node2D[] vertices = CreateVertices();
            Node2D[][] elementConnectivities = CreateCellConnectivities(vertices);
            var cellTypes = new CellType2D[elementConnectivities.Length];
            for (int i = 0; i < cellTypes.Length; ++i) cellTypes[i] = CellType2D.Quad4;
            return (vertices, cellTypes, elementConnectivities);
        }

        private Node2D[] CreateVertices()
        {
            var vertices = new Node2D[vertexRows * vertexColumns];
            int id = 0;
            for (int row = 0; row < vertexRows; ++row)
            {
                for (int col = 0; col < vertexColumns; ++col)
                {
                    vertices[id] = new Node2D(id, minX + col * dx, minY + row * dy);
                    ++id;
                }
            }
            return vertices;
        }

        private Node2D[][] CreateCellConnectivities(Node2D[] allVertices)
        {
            var cellConnectivity = new Node2D[cellRows * cellColumns][];
            for (int row = 0; row < cellRows; ++row)
            {
                for (int col = 0; col < cellColumns; ++col)
                {
                    int firstVertex = row * vertexColumns + col;
                    Node2D[] verticesOfCell = { allVertices[firstVertex], allVertices[firstVertex+1],
                        allVertices[firstVertex + vertexColumns + 1], allVertices[firstVertex + vertexColumns] };
                    cellConnectivity[row * cellColumns + col] = verticesOfCell; // row major
                }
            }
            return cellConnectivity;
        }
    }
}
