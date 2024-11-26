using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

using UnityEngine.Localization;

public static class DateFormatter : object

{
    public static LocalizedString formatDate(DateTime dateTime, LocalizedString dateFormat) {

        if (dateTime == null) {
            dateTime = DateTime.Now;
        }
            dateFormat.Arguments = new object[] { dateTime };
        return dateFormat;
    }

}
