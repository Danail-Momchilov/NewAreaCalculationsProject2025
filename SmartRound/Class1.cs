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

            return (number + valueToAdd) / 100;
        }
        
        public double multiplyValues(double value1, double value2)
        {
            value1 = value1 * 100;
            value2 = value2 * 100;
            
            return (value1 * value2) / 10000;
        }
    }
}
