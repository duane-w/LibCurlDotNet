using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Samples
{
    class Program
    {
        static void Main(string[] args)
        {
            //BookPost bp = new BookPost();
            //bp.Perform();

            PostCallback pcb = new PostCallback();
            pcb.Perform();

            //PIX p = new PIX();
            //p.Perform();
        }
    }
}
