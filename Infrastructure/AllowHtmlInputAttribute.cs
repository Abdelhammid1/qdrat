using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System;

namespace QdratNew.Infrastructure
{
  

    [AttributeUsage(AttributeTargets.Property)]
    public class AllowHtmlInputAttribute : Attribute, IPropertyValidationFilter
    {
        public bool ShouldValidateEntry(ValidationEntry entry, ValidationEntry parentEntry)
        {
            // ❌ تعطيل التحقق لمعالجة HTML
            return false;
        }
    }

}
