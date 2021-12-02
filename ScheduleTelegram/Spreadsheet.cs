﻿using System;
using System.Collections.Generic;

using System.Text.RegularExpressions;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

using System.Threading.Tasks;
using System.IO;
using System.Threading;

using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

using Google.Apis.Auth.OAuth2;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;

using ScheduleTelegram;

namespace ScheduleTelegram
{

    public class Spreadsheet
    {
        static string[] Scopes = { SheetsService.Scope.SpreadsheetsReadonly };
        static string ApplicationName = "Google Sheets API .NET Quickstart";


        public class Lessons
        {
            public string MajorDimension { get; set; }
            public string Range { get; set; }
            public List<List<string>> Values { get; set; }
            public object ETag { get; set; }
        }

        public class LessonsReformatted
        {
            public string ClassOne { get; set; }
            public string ClassTwo { get; set; }
            public string ClassThree { get; set; }
            public string ClassFour { get; set; }
            public string ClassFive { get; set; }
            public string ClassSix { get; set; }
            public string ClassSeven { get; set; }
            public string ClassEight { get; set; }
        }




        // Ключевой метод для всей этой штуки. Получаем данные с таблицы и сохраняем их в data.txt
        public static void GetScheduleData(string commandText)
        {
            UserCredential credential;

            using (var stream =
                new FileStream("credentials.json", FileMode.Open, FileAccess.Read))
            {
                // Токен в JSON, нужен для авторизации и чтения
                // НЕ ТРОГАТЬ!
                string credPath = "token.json";
                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.FromStream(stream).Secrets,
                    Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(credPath, true)).Result;
            }

            // Подключение АПИ, что-то чисто Гугловское
            var service = new SheetsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            });

            var serializerOpt = new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(UnicodeRanges.All),
                WriteIndented = true,
            };


            Dates ScheduleDate = new Dates();
            // Define request parameters.
            String spreadsheetId = "1qn5WoQTrMtmCtkhz9Lrjy88ZnLFHM19dCha2WuBBZ4k";
            String range = Dates.GetNeededDate(commandText) + "!B3:P10";
            SpreadsheetsResource.ValuesResource.GetRequest request =
                    service.Spreadsheets.Values.Get(spreadsheetId, range);
            request.MajorDimension = SpreadsheetsResource.ValuesResource.GetRequest.MajorDimensionEnum.COLUMNS;
            ValueRange response = request.Execute();
            string jsonString = JsonSerializer.Serialize(response, serializerOpt);
            System.IO.File.WriteAllText("testjson", jsonString);
            IList<IList<Object>> values = response.Values;

            int i = 0;
            foreach (List<Object> subList in values)
            {
                i++;
                string tempFileName = "grade" + i.ToString() + ".json";

                LessonsReformatted lessons = new LessonsReformatted
                {
                    ClassOne = (string)subList[0],
                    ClassTwo = (string)subList[1],
                    ClassThree = (string)subList[2],
                    ClassFour = (string)subList[3],
                    ClassFive = (string)subList[4],
                    ClassSix = (string)subList[5],
                    ClassSeven = (string)subList[1],
                    ClassEight = (string)subList[1]
                };

                string jsonTest = JsonSerializer.Serialize<LessonsReformatted>(lessons, serializerOpt);
                Console.WriteLine(jsonTest);
                System.IO.File.WriteAllText(tempFileName, jsonTest);
                ParseAndFix(tempFileName);
                RenameSubjects(tempFileName);
            }
            





        }

        public static string CommandCheck(string textMessage)
        {
            string fileName;


            switch (textMessage)
            {
                case "/today":
                    fileName = "today.txt";
                    return fileName;
                case "/tomorrow":
                    fileName = "tomorrow.txt";
                    return fileName;
                default:
                    goto case "/today";
            }


        }

        // Небольшой regex для переименовывания нестабильно названных предметов в расписании
        public static void RenameSubjects(string tempFilePath)
        {

            //fileName = CommandCheck(textMessage);

            StreamReader reader = new StreamReader(tempFilePath);
            string content = reader.ReadToEnd();
            reader.Close();

            string[] initialSubjectArray = new string[]
            {
                "(Английски)",
                "(Литер.чт)\b",
                "Матем",
                "(Окр мир)\b",
                "(Техн..)\b",
                "(Русский)\b",
                "(Литер.)\b",
                "(литер)",
                "(экономика)",
                "фин.гр.  ",
                "(эл.англ.яз)\b",
                "эл.англ,",
                "консультация [()]химия[()]",
                "(физика)",
                "(химия)",
                "(история)",
                @"^/n",
                "акт зал",
                "акт.зал"
            };

            string[] replacementSubjectArray = new string[]
            {
                "Английский язык",
                "Литературное чтение",
                "Математика",
                "Окружающий мир",
                "Технология",
                "Русский язык",
                "Литература",
                "Литература (элективы)",
                "Экономика",
                "Финансовая грамотность",
                "Английский язык (электив)",
                "Английский язык (электив)",
                "Консультация (химия)",
                "Физика",
                "Химия",
                "История",
                "Окно",
                "",
                ""
            };

            for (int i = 0; i < initialSubjectArray.Length; i++)
            {
                content = Regex.Replace(content, initialSubjectArray[i], replacementSubjectArray[i]);
            }

            StreamWriter writer = new StreamWriter(tempFilePath);
            writer.Write(content);
            writer.Close();
        }

        // regex для форматирования сообщения. Удаляет названия и номера кабинетов, лишние пробелы и переносы строк
        public static void ParseAndFix(string tempFileName)
        {
            //string regexPattern = @"\s[Кк].*";
            string regexPattern = @"([Кк]аб.)\s?\w+";
            Regex regex = new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);

            //string fileName = CommandCheck(textMessage);

            StreamReader reader = new StreamReader(tempFileName);
            string content = reader.ReadToEnd();
            reader.Close();

            content = Regex.Replace(content, regexPattern, " ");
            content = Regex.Replace(content, @"/", " ");
            content = Regex.Replace(content, @"\b\s{2,}\n", "");
            content = Regex.Replace(content, @"\s+$", "\n");
            content = Regex.Replace(content, @"\s+\n", "\n");
            content = Regex.Replace(content, @"\s{2,}\b", " ");
            content = Regex.Replace(content, @"\b\s{2,}", "");

            StreamWriter writer = new StreamWriter(tempFileName);
            writer.Write(content);
            writer.Close();
        }
        public static string Schedule(string textMessage)
        {
            var serializerOpt = new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(UnicodeRanges.All),
                WriteIndented = true,
            };
            //System.IO.File.Delete("today.txt");
            //System.IO.File.Delete("tomorrow.txt");

            CommandCheck(textMessage);
            GetScheduleData(textMessage);

            string messageReply;

            switch (textMessage)
            {
                case "/today":
                    using (StreamReader middleData = new("grade14.json", true))
                    {
                        string text = middleData.ReadToEnd();
                        LessonsReformatted lessons = JsonSerializer.Deserialize<LessonsReformatted>(text);
                        messageReply = ($"{lessons.ClassOne} \n {lessons.ClassTwo}\n {lessons.ClassThree}\n {lessons.ClassFour}\n {lessons.ClassFive}\n {lessons.ClassSix}\n {lessons.ClassSeven}\n {lessons.ClassEight}");
                        
                    }
                    return messageReply;

                case "/tomorrow":
                    using (StreamReader middleData = new("grade14.json", true))
                    {
                        string text = middleData.ReadToEnd();
                        LessonsReformatted lessons = JsonSerializer.Deserialize<LessonsReformatted>(text);
                        messageReply = ($"{lessons.ClassOne} \n {lessons.ClassTwo}\n {lessons.ClassThree}\n {lessons.ClassFour}\n {lessons.ClassFive}\n {lessons.ClassSix}\n {lessons.ClassSeven}\n {lessons.ClassEight}");
                    }
                    return messageReply;
                default:
                    goto case "/today";
            }
        }
    }
}