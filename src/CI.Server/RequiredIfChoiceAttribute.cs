using System;
using System.ComponentModel.DataAnnotations;

namespace Helium.CI.Server
{
    // https://stackoverflow.com/questions/52321148/conditional-validation-in-mvc-net-core-requiredif
    public class RequiredIfChoiceAttribute : RequiredAttribute
    {
        private string PropertyName { get; set; }
        private object DesiredValue { get; set; }

        public RequiredIfChoiceAttribute(string propertyName, string desiredValue)
        {
            PropertyName = propertyName;
            DesiredValue = desiredValue;
        }

        protected override ValidationResult IsValid(object value, ValidationContext context)
        {
            var instance = context.ObjectInstance;
            var type = instance.GetType();
            var propertyValue = type.GetProperty(PropertyName)?.GetValue(instance, null);
            
            if(propertyValue?.ToString() == DesiredValue.ToString()) {
                return base.IsValid(value, context);
            }
            else {
                return ValidationResult.Success;
            }
        }
    }
}