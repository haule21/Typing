using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TypingApp.Models;

namespace TypingApp.Services
{
    public static class OcrResultParser
    {
        public static string Format(List<OcrWordRegion> regions)
        {
            if (regions == null || !regions.Any()) return string.Empty;

            // 주의: 변환된 결과에 대해 group by 나 order by를 수행하지 않고
            // 엔진이 인식한 자연스러운 순서(Natural Order)를 그대로 유지합니다.
            return string.Join(Environment.NewLine, regions.Select(r => r.Text));
        }
    }
}