import {Layout} from "@/components/Layout";
import {InteractionViewLayout} from "@/components/InteractionViewLayout";
import {Card} from "@/components/Card";
import {Headline} from "@/components/Headline";

export default function Home() {
  return (
    <Layout>
      <Headline>Argus</Headline>
      <InteractionViewLayout>
        <Card>
          Input
        </Card>
        <Card>
          Results
        </Card>
      </InteractionViewLayout>
    </Layout>
  )
}
