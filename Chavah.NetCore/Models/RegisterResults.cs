﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace BitShuva.Chavah.Models
{
    public class RegisterResults
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public bool IsAlreadyRegistered { get; set; }
        public bool NeedsConfirmation { get; set; }
    }
}