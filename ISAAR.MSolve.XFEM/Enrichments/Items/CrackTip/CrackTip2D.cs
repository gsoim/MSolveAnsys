﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ISAAR.MSolve.XFEM.Elements;
using ISAAR.MSolve.XFEM.Enrichments.Functions;
using ISAAR.MSolve.XFEM.Entities;
using ISAAR.MSolve.XFEM.Entities.FreedomDegrees;
using ISAAR.MSolve.XFEM.Geometry.CoordinateSystems;
using ISAAR.MSolve.XFEM.Geometry.Shapes;
using ISAAR.MSolve.XFEM.Geometry.Mesh;
using ISAAR.MSolve.XFEM.Geometry.Descriptions;
using ISAAR.MSolve.XFEM.Materials;
using ISAAR.MSolve.XFEM.CrackPropagation.Jintegral;


namespace ISAAR.MSolve.XFEM.Enrichments.Items.CrackTip
{
    // TODO: there should be some linking between the crack tips and the crack body, outside sharing the same curve 
    // object.
    class CrackTip2D: AbstractEnrichmentItem2D
    {
        // TODO: The tip itself should not have to know where on the crack it is.
        // There must be a master crack class that knows where the tips, bodies and junctions are.
        public enum TipCurvePosition
        {
            CurveStart, CurveEnd, Both
        }

        private readonly TipCurvePosition tipPosition;
        private readonly IGeometryDescription2D discontinuity;
        private readonly ITipEnrichmentAreaStrategy enrichmentAreaStrategy;
        private readonly IAuxiliaryStates auxiliaryStatesStrategy;
        private readonly ISIFCalculator sifCalculationStrategy;

        public IMesh2D<XNode2D, XContinuumElementCrack2D> Mesh { get; }

        // The next properties/fields need to be updated at each analysis step.
        public IReadOnlyList<XContinuumElementCrack2D> TipElements { get; private set; }
        public TipCoordinateSystem TipSystem { get; private set; }
        public ICartesianPoint2D TipCoordinates { get; private set; }

        public CrackTip2D(TipCurvePosition tipPosition, IGeometryDescription2D discontinuity,
            ITipEnrichmentAreaStrategy enrichmentAreaStrategy, IAuxiliaryStates auxiliaryStatesStrategy,
            ISIFCalculator sifCalculationStrategy)
        {
            this.tipPosition = tipPosition;
            this.discontinuity = discontinuity;
            this.enrichmentAreaStrategy = enrichmentAreaStrategy;
            this.auxiliaryStatesStrategy = auxiliaryStatesStrategy;
            this.sifCalculationStrategy = sifCalculationStrategy;

            this.EnrichmentFunctions = new IEnrichmentFunction2D[]
            {
                //new IsotropicBrittleTipFunctions2DAlternative.Func1(this),
                //new IsotropicBrittleTipFunctions2DAlternative.Func2(this),
                //new IsotropicBrittleTipFunctions2DAlternative.Func3(this),
                //new IsotropicBrittleTipFunctions2DAlternative.Func4(this)
                new IsotropicBrittleTipFunctions2D.Func1(this),
                new IsotropicBrittleTipFunctions2D.Func2(this),
                new IsotropicBrittleTipFunctions2D.Func3(this),
                new IsotropicBrittleTipFunctions2D.Func4(this)
            };
            this.DOFs = new ArtificialDOFType[]
            {
                new ArtificialDOFType(EnrichmentFunctions[0], StandardDOFType.X),
                new ArtificialDOFType(EnrichmentFunctions[0], StandardDOFType.Y),
                new ArtificialDOFType(EnrichmentFunctions[1], StandardDOFType.X),
                new ArtificialDOFType(EnrichmentFunctions[1], StandardDOFType.Y),
                new ArtificialDOFType(EnrichmentFunctions[2], StandardDOFType.X),
                new ArtificialDOFType(EnrichmentFunctions[2], StandardDOFType.Y),
                new ArtificialDOFType(EnrichmentFunctions[3], StandardDOFType.X),
                new ArtificialDOFType(EnrichmentFunctions[3], StandardDOFType.Y),
            };

            UpdateTransform();
        }

        // TODO: this needs reworking. E.g. making sure there are no identical points might already be 
        // done by the geometry class
        public override IReadOnlyList<ICartesianPoint2D> IntersectionPointsForIntegration(XContinuumElement2D element)
        {
            var uniquePoints = new HashSet<ICartesianPoint2D>(element.Nodes);
            
            foreach (ICartesianPoint2D point in discontinuity.IntersectionWith(element))
            {
                uniquePoints.Add(point);
            }

            if (tipPosition == TipCurvePosition.CurveEnd) uniquePoints.Add(discontinuity.EndPoint);
            else if (tipPosition == TipCurvePosition.CurveStart) uniquePoints.Add(discontinuity.StartPoint);
            else throw new NotImplementedException("For now the tip can only be located at one end of the curve");

            return new List<ICartesianPoint2D>(uniquePoints);
        }

        private void UpdateTransform()
        {
            if (tipPosition == TipCurvePosition.CurveEnd)
            {
                TipCoordinates = discontinuity.EndPoint;
                TipSystem = new TipCoordinateSystem(TipCoordinates, discontinuity.EndPointOrientation());
            }
            else if (tipPosition == TipCurvePosition.CurveStart)
            {
                TipCoordinates = discontinuity.StartPoint;
                TipSystem = new TipCoordinateSystem(TipCoordinates, discontinuity.StartPointOrientation());
            }
            else
            {
                throw new NotImplementedException("For now the tip can only be located at one end of the curve");
            }
        }

        // TODO: what happens if the tip is on an element's edge/node?
        private void UpdateTipElements(ICartesianPoint2D newTip)
        {
            IReadOnlyList<XContinuumElementCrack2D> tipElements = Mesh.FindElementsContainingPoint(newTip, 
                TipElements[0]);
            if (tipElements.Count == 0) throw new NotImplementedException("New tip is outside of domain");
            else TipElements = tipElements;
        }

        private void ComputeSIFS(Model2D model, double[] totalFreeDisplacements, double[] totalConstrainedDisplacements,
            out double sifMode1, out double sifMode2)
        {
            double interactionIntegralMode1 = 0.0, interactionIntegralMode2 = 0.0;
            foreach(var pair in FindJintegralElementsAndNodalWeights())
            {
                XContinuumElementCrack2D element = pair.Key;
                double[] nodalWeights = pair.Value;
                double[] elementDisplacements = model.DofEnumerator.ExtractDisplacementVectorOfElementFromGlobal(
                    element, totalFreeDisplacements, totalConstrainedDisplacements);

                double partialIntegralMode1, partialIntegralMode2;
                element.ComputeInteractionIntegrals(TipSystem, auxiliaryStatesStrategy, elementDisplacements,
                    nodalWeights, out partialIntegralMode1, out partialIntegralMode2);

                interactionIntegralMode1 += partialIntegralMode1;
                interactionIntegralMode2 += partialIntegralMode2;
            }

            sifMode1 = sifCalculationStrategy.CalculateSIF(interactionIntegralMode1);
            sifMode2 = sifCalculationStrategy.CalculateSIF(interactionIntegralMode2);
        }

        private IReadOnlyDictionary<XContinuumElementCrack2D, double[]> FindJintegralElementsAndNodalWeights()
        {
            Circle2D outerContour = 
                new Circle2D(TipCoordinates, enrichmentAreaStrategy.ComputeRadiusOfJintegralOuterContour(this));
            IReadOnlyList<XContinuumElementCrack2D> intersectedElements = 
                Mesh.FindElementsIntersectedByCircle(outerContour, TipElements[0]);

            var elementsAndWeights = new Dictionary<XContinuumElementCrack2D, double[]>();
            foreach (var element in intersectedElements)
            {
                // The relative position of the circle and the nodes was already calculated when checking the
                // circle-element intersection, but that method should be decoupled from assigning the nodal 
                // weights, even at the cost of some duplicate operations. What could be done more efficiently is 
                // caching the nodes and weights already processed by previous elements, but even then the cost of
                // processing each node will be increased by the lookup.
                double[] nodalWeights = new double[element.Nodes.Count];
                for (int nodeIdx = 0; nodeIdx < element.Nodes.Count; ++nodeIdx)
                {
                    if (outerContour.FindRelativePositionOfPoint(element.Nodes[nodeIdx]) == CirclePointPosition.Outside)
                    {
                        nodalWeights[nodeIdx] = 0.0;
                    }
                    else // Node lies inside or exactly on the circle
                    {
                        nodalWeights[nodeIdx] = 1.0;
                    }
                }
                elementsAndWeights.Add(element, nodalWeights);
            }
            return elementsAndWeights;
        }
    }
}
