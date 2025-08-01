using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AreaCalculations
{
    internal class AritmeticAssistant
    {
        public AritmeticAssistant() { }
        public double addToArea(double number, double valueToAdd)
        {
            number = number * 100;
            valueToAdd = valueToAdd * 100;

            return Math.Round((number + valueToAdd) / 100, 2, MidpointRounding.AwayFromZero);
        }
        
        public double multiplyValues(double value1, double value2)
        {
            value1 = value1 * 100;
            value2 = value2 * 100;
            
            return Math.Round((value1 * value2) / 10000, 2, MidpointRounding.AwayFromZero);
        }
        
        public double divideValue(double value, double divisor)
        {
            value = value * 100;
            
            return Math.Round(value / divisor / 100, 2, MidpointRounding.AwayFromZero);
        }
    }
}
