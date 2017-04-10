﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ISAAR.MSolve.XFEM.Enrichments.Items.CrackTip;
using ISAAR.MSolve.XFEM.Geometry.CoordinateSystems;
using ISAAR.MSolve.XFEM.Materials;


namespace ISAAR.MSolve.XFEM.CrackPropagation.Jintegral
{
    interface IAuxiliaryStates
    {
        AuxiliaryStatesTensors ComputeTensorsAt(ICartesianPoint2D globalIntegrationPoint, 
            IFiniteElementMaterial2D materialPoint, TipCoordinateSystem tipCoordinateSystem);
    }
}
