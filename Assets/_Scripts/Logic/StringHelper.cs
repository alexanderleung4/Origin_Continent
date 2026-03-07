using UnityEngine;

public static class StringHelper
{
    // 写一个静态方法，传入一个 int 类型的金币数量
    // 如果金币大于 10000，则返回以 k 为单位的字符串并保留一位小数 (例如 15500 返回 "15.5k")
    // 如果小于 10000，则直接返回数字的字符串
    public static string FormatCurrency(int amount)
    {
        if (amount >= 10000)
        {
            float formattedAmount = amount / 1000f;
            return $"{formattedAmount:F1}k";
        }
        return amount.ToString();
    }
}