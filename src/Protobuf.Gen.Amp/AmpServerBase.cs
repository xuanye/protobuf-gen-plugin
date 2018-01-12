using System;
using System.Text;
using Google.Protobuf.Compiler;
using Google.Protobuf.Reflection;
using Protobuf.Gen.Core;

namespace Protobuf.Gen.Amp
{
    public class AmpServerBase : AmpPluginBase
    {
        protected override void GenerateByEachFile(FileDescriptorProto protofile, CodeGeneratorResponse response)
        {
            bool genericServer;
            protofile.Options.CustomOptions.TryGetBool(DotBPEOptions.DISABLE_GENERIC_SERVICES_SERVER, out genericServer);
            if (genericServer)
            {
                return;
            }
            if (protofile.Service == null || protofile.Service.Count <= 0) return;
            //生成文件头
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("// Generated by the protocol buffer compiler.  DO NOT EDIT!");
            sb.AppendLine($"// source: {protofile.Name}");
            //还可以生成注释

            sb.AppendLine("#region Designer generated code");
            sb.AppendLine("");
            sb.AppendLine("using System; ");
            sb.AppendLine("using System.Threading.Tasks; ");
            sb.AppendLine("using DotBPE.Rpc; ");
            sb.AppendLine("using DotBPE.Protocol.Amp; ");
            sb.AppendLine("using Google.Protobuf; ");
            sb.AppendLine("");

            string ns = Utils.GetFileNamespace(protofile);
            sb.AppendLine("namespace " + ns + " {");
            //生成代码
            foreach (ServiceDescriptorProto t in protofile.Service)
            {
                t.Options.CustomOptions.TryGetBool(DotBPEOptions.DISABLE_GENERIC_SERVICES_SERVER, out genericServer);
                if (genericServer)
                {
                    continue;
                }

                sb.AppendLine("");
                sb.AppendLine("//start for class Abstract"+t.Name);
                GenerateServiceForServer(t, sb);
                sb.AppendLine("//end for class Abstract"+t.Name);
            }
            sb.AppendLine("}\n");
            sb.AppendLine("#endregion\n");

            var nfile = new CodeGeneratorResponse.Types.File
            {
                Name = Utils.GetFileName(protofile.Name) + "Server.cs",
                Content = sb.ToString()
            };
            response.File.Add(nfile);
        }

        private static void GenerateServiceForServer(ServiceDescriptorProto service, StringBuilder sb)
        {
            int serviceId;
            bool hasServiceId = service.Options.CustomOptions.TryGetInt32(DotBPEOptions.SERVICE_ID, out serviceId);
            if(!hasServiceId || serviceId<=0){
                throw new Exception("Service="+service.Name+" ServiceId not found");
            }
            if(serviceId>=ushort.MaxValue){
                throw new Exception("Service="+service.Name+ "ServiceId too large" );
            }

            sb.AppendFormat("   public abstract class {0}Base : ServiceActor \n", service.Name);
            sb.AppendLine("   {");

            sb.AppendLine("      protected override int ServiceId => " + serviceId+";");



            StringBuilder sbIfState = new StringBuilder();

            //循环方法
            foreach (var method in service.Method)
            {
                int msgId ;
                bool hasMsgId= method.Options.CustomOptions.TryGetInt32(DotBPEOptions.MESSAGE_ID,out msgId);
                if(!hasMsgId || msgId<=0){
                    throw new Exception("Service"+service.Name+"."+method.Name+" ' MessageId  not found ");
                }
                if(msgId>=ushort.MaxValue){
                    throw new Exception("Service" + service.Name+"."+method.Name+" is too large");
                }
                //异步方法
                string outType = Utils.GetTypeName(method.OutputType);
                string inType =  Utils.GetTypeName(method.InputType);


                sb.AppendLine("      //调用委托");
                sb.AppendLine($"      private async Task<AmpMessage> Process{method.Name}Async(AmpMessage req)");
                sb.AppendLine("      {");
                //添加判断req.Data == null;
                sb.AppendLine($"         {inType} request = null;");
                sb.AppendLine("");
                sb.AppendLine("         if(req.Data == null ){");
                sb.AppendLine($"            request = new {inType}();");
                sb.AppendLine("         }");
                sb.AppendLine("         else {");
                sb.AppendLine($"            request = {inType}.Parser.ParseFrom(req.Data);");
                sb.AppendLine("         }");
                sb.AppendLine("");
                sb.AppendLine($"         var result = await {method.Name}Async(request);");
                sb.AppendLine("         var response = AmpMessage.CreateResponseMessage(req.ServiceId, req.MessageId);");
                sb.AppendLine("         response.Code = result.Code;");
                sb.AppendLine("         if( result.Data !=null )");
                sb.AppendLine("         {");
                sb.AppendLine("             response.Data = result.Data.ToByteArray();");
                sb.AppendLine("         }");
                sb.AppendLine("         return response;");
                sb.AppendLine("      }");

                sb.AppendLine();
         

                sb.AppendLine("      //抽象方法");
                sb.AppendLine($"      public abstract Task<RpcResult<{outType}>> {method.Name}Async({inType} request);");

                //拼装if调用语句
                sbIfState.AppendFormat("            //方法{0}.{1}\n", service.Name,method.Name);
                sbIfState.AppendLine("            case " + msgId+": return this.Process"+method.Name+"Async(req);");
            }

            //循环方法end
            //生成主调用代码
            sb.AppendLine("      public override Task<AmpMessage> ProcessAsync(AmpMessage req)");
            sb.AppendLine("      {");

            sb.AppendLine("         switch(req.MessageId){");
            sb.Append(sbIfState);
            sb.AppendLine("            default: return base.ProcessNotFoundAsync(req);");
            sb.AppendLine("         }"); //end switch case
            sb.AppendLine("      }");


            sb.AppendLine("   }");
            //类结束
        }
    }
}
