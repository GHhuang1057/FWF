using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using FlashWorkflowFramework.Core.Models;

namespace FlashWorkflowFramework.Core.Utils
{
    public static class WorkflowParser
    {
        public static async Task<Workflow> ParseAsync(string workflowPath)
        {
            if (!File.Exists(workflowPath))
            {
                throw new FileNotFoundException($"工作流文件未找到: {workflowPath}");
            }

            var workflow = new Workflow();
            
            await Task.Run(() =>
            {
                var xmlDoc = new XmlDocument();
                xmlDoc.Load(workflowPath);
                
                // 添加调试信息
                ConsoleLogger.Info($"已加载XML工作流文件: {workflowPath}");
                
                // 使用不区分大小写的查询
                var rootNode = xmlDoc.SelectSingleNode("//*[local-name()='Workflow']");
                if (rootNode == null)
                {
                    // 尝试直接查找Workflow节点（区分大小写）
                    rootNode = xmlDoc.SelectSingleNode("Workflow");
                    if (rootNode == null)
                    {
                        throw new InvalidOperationException("无效的工作流文件格式：缺少Workflow根节点");
                    }
                }
                
                // 解析工作流属性
                workflow.Name = GetAttribute(rootNode, "Name", "未命名工作流");
                workflow.Version = GetAttribute(rootNode, "Version", "1.0");
                
                // 解析变量
                var variablesNode = rootNode.SelectSingleNode(".//*[local-name()='Variables']");
                if (variablesNode != null)
                {
                    foreach (XmlNode varNode in variablesNode.SelectNodes(".//*[local-name()='Variable']"))
                    {
                        var name = GetAttribute(varNode, "Name");
                        var value = GetAttribute(varNode, "Value");
                        
                        if (!string.IsNullOrEmpty(name))
                        {
                            workflow.Variables[name] = value ?? string.Empty;
                        }
                    }
                }
                
                // 解析步骤
                var stepsNode = rootNode.SelectSingleNode(".//*[local-name()='Steps']");
                if (stepsNode != null)
                {
                    foreach (XmlNode stepNode in stepsNode.SelectNodes(".//*[local-name()='Step']"))
                    {
                        var step = new WorkflowStep
                        {
                            Type = GetAttribute(stepNode, "Type"),
                            Name = GetAttribute(stepNode, "Name"),
                            Condition = GetAttribute(stepNode, "Condition")
                        };
                        
                        if (string.IsNullOrEmpty(step.Type))
                        {
                            throw new InvalidOperationException("工作流步骤必须指定Type属性");
                        }
                        
                        // 解析步骤参数
                        foreach (XmlNode paramNode in stepNode.ChildNodes)
                        {
                            if (paramNode.NodeType == XmlNodeType.Element && !string.IsNullOrEmpty(paramNode.InnerText))
                            {
                                step.Parameters[paramNode.Name] = paramNode.InnerText;
                            }
                        }
                        
                        workflow.Steps.Add(step);
                    }
                }
            });
            
            ConsoleLogger.Info($"解析工作流完成: {workflow.Name}，共 {workflow.Steps.Count} 个步骤");
            return workflow;
        }
        
        private static string GetAttribute(XmlNode node, string name, string defaultValue = "")
        {
            if (node.Attributes == null)
                return defaultValue;
                
            // 不区分大小写查找属性
            foreach (XmlAttribute attr in node.Attributes)
            {
                if (string.Equals(attr.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return attr.Value;
                }
            }
            
            return defaultValue;
        }
    }
}