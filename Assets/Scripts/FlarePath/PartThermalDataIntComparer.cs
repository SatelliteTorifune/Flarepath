using System.Collections.Generic;

public class PartThermalDataIntComparer : IComparer<PartThermalData>
{
    public int Compare(PartThermalData a, PartThermalData b)
    {
        if (a.part.Temperature < b.part.Temperature)
        {
            return -1;
        }

        if (a.part.Temperature == b.part.Temperature)
        {
            return 0;
        }
        return 1;
    }
}