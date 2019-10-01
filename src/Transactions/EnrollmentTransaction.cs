using System;
using System.Collections.Generic;
using System.Text;

namespace NeoFx.Models
{

    [Obsolete]
    public class EnrollmentTransaction : Transaction
    {
        public override TransactionType Type => TransactionType.EnrollmentTransaction;

        // public ECPoint PublicKey
    }
}
