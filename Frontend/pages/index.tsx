import {Layout} from "@/components/Layout";
import {InteractionViewLayout} from "@/components/InteractionViewLayout";
import {Card} from "@/components/Card";
import {Headline} from "@/components/Headline";
import {CardTitle} from "@/components/CardTitle";

export default function Home() {
  return (
    <Layout>
      <Headline>Argus</Headline>
      <InteractionViewLayout>
        <Card>
          <CardTitle>
            Your images
          </CardTitle>

        </Card>
        <Card>
          <CardTitle>
            Potential copyright violations
          </CardTitle>
        </Card>
      </InteractionViewLayout>
    </Layout>
  )
}
